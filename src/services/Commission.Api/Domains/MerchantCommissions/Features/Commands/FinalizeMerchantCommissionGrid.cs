
namespace Commission.Api.Domains.MerchantCommissions.Features.Commands;

/// <summary>
/// US4 — merchant grid'ini finalize eder (B kararı, gateway-otoriter). Bütünlük: eksik hücre YOK
/// (IsMissing yok). Tavan kuralı (BelowBankCeiling) 2026-08-11'de finalize'dan ÇIKARILDI — yön/anlam
/// tartışmalı, yeniden tasarlanacak (bkz. Obsidian DropShop/Yapılacaklar); listede bilgi amaçlı durur.
/// Geçerse grid Ready olur ve <c>MerchantCommissionGridReady</c> yayınlanır (aynı <c>[Transactional]</c>
/// = outbox, D13). Draft'ta event/Excel YOK (erken tetikleme önlenir). İdempotent: zaten Ready ise event
/// tekrar yayılmaz.
/// </summary>
public static class FinalizeMerchantCommissionGrid
{
    public record FinalizeMerchantCommissionGridCommand(Guid MerchantId);

    public class FinalizeMerchantCommissionGridResponse
    {
        public string Status { get; set; } = string.Empty;
    }

    [Transactional]
    public class FinalizeMerchantCommissionGridCommandHandler
    {
        public async Task<FeatureObjectResultModel<FinalizeMerchantCommissionGridResponse>> Handle(
            FinalizeMerchantCommissionGridCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            // Bütünlük için enriched satırları hesapla (GetMerchantCommissions ile aynı mantık).
            var merchantCommissions = await session.Query<MerchantCommission>()
                .Where(c => c.MerchantId == cmd.MerchantId && !c.IsDeleted)
                .ToListAsync(ct);

            var bankCommissions = await session.Query<BankCommission>()
                .Where(c => !c.IsDeleted)
                .ToListAsync(ct);

            var bankByCriteria = bankCommissions
                .GroupBy(b => b.Criteria)
                .ToDictionary(g => g.Key, g => (Min: g.Min(x => x.Rate), Max: g.Max(x => x.Rate)));

            var merchantByCriteria = merchantCommissions.ToDictionary(c => c.Criteria);

            var allCriteria = new HashSet<Criteria>(merchantByCriteria.Keys);
            allCriteria.UnionWith(bankByCriteria.Keys);

            if (allCriteria.Count == 0)
                return Error(CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND);

            foreach (var criteria in allCriteria)
            {
                merchantByCriteria.TryGetValue(criteria, out var mc);

                // Eksik hücre → RET.
                if (mc?.Rate == null)
                    return Error(CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED);
            }

            var grid = await session.LoadAsync<MerchantCommissionGrid>(cmd.MerchantId, ct)
                       ?? MerchantCommissionGrid.CreateDraft(cmd.MerchantId);

            var wasReady = grid.Status == GridStatus.Ready;
            grid.MarkReady();
            session.Store(grid);

            // İdempotent: zaten Ready ise event tekrar yayılmaz (tek-yön).
            if (!wasReady)
                await bus.PublishAsync(new Shared.IntegrationEvents.MerchantCommissionGridReady(cmd.MerchantId));

            return FeatureObjectResultModel<FinalizeMerchantCommissionGridResponse>.Ok(
                new FinalizeMerchantCommissionGridResponse { Status = GridStatus.Ready.ToString() });
        }

        private static FeatureObjectResultModel<FinalizeMerchantCommissionGridResponse> Error(string code) =>
            FeatureObjectResultModel<FinalizeMerchantCommissionGridResponse>.Error(new MessageItem
            {
                Property = "Grid",
                Code = code
            });
    }
}

public static class FinalizeMerchantCommissionGridEndpoint
{
    public static RouteGroupBuilder FinalizeMerchantCommissionGridGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/finalize",
                async ([FromBody] FinalizeMerchantCommissionGrid.FinalizeMerchantCommissionGridCommand cmd, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<FinalizeMerchantCommissionGrid.FinalizeMerchantCommissionGridResponse>>(cmd);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("FinalizeMerchantCommissionGrid")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.CommissionWrite)
            .Produces<FinalizeMerchantCommissionGrid.FinalizeMerchantCommissionGridResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        return group;
    }
}
