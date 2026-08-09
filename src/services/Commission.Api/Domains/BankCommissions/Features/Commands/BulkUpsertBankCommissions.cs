
namespace Commission.Api.Domains.BankCommissions.Features.Commands;

/// <summary>
/// Grid kaydı: seçilen banka + doldurulan hücreler tek atomik işlemde upsert edilir.
/// Mevcut (BankCode, Criteria) → UpdateRate; yok → Create. Tek-tek POST korunur (geriye uyum).
/// </summary>
public static class BulkUpsertBankCommissions
{
    public record CriteriaDto(
        string CardBrand,
        string CardType,
        string TransactionRegion,
        int InstallmentCount);

    public record Item(CriteriaDto Criteria, decimal Rate);

    public record BulkUpsertBankCommissionsCommand(string BankCode, List<Item> Items);

    public class BulkUpsertBankCommissionsResponse
    {
        public int Created { get; set; }
        public int Updated { get; set; }
    }

    [Transactional]
    public class BulkUpsertBankCommissionsCommandHandler
    {
        public async Task<FeatureObjectResultModel<BulkUpsertBankCommissionsResponse>> Handle(
            BulkUpsertBankCommissionsCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            // 1) Banka Code ile yüklenir; yoksa/pasifse hata.
            var bank = await session.Query<Bank>()
                .Where(b => b.Code == cmd.BankCode && !b.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (bank is null || !bank.IsActive)
            {
                return FeatureObjectResultModel<BulkUpsertBankCommissionsResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.BankCode),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });
            }

            var supported = bank.SupportedInstallments.ToHashSet();

            // Mevcut komisyonlar (aynı banka) belleğe: nested VO LINQ'tan kaçın, kardinalite düşük.
            var existing = await session.Query<BankCommission>()
                .Where(c => c.BankCode == cmd.BankCode && !c.IsDeleted)
                .ToListAsync(ct);

            var response = new BulkUpsertBankCommissionsResponse();

            // Aynı kombinasyon bulk içinde iki kez gelirse son değer geçerli: kriterle bellekte izle.
            var touched = new Dictionary<Criteria, BankCommission>();

            foreach (var item in cmd.Items ?? new List<Item>())
            {
                // Taksit bankanın desteklediği sette değilse reddet (atomik: hepsi geri sarılır).
                if (!supported.Contains(item.Criteria.InstallmentCount))
                {
                    return FeatureObjectResultModel<BulkUpsertBankCommissionsResponse>.Error(new MessageItem
                    {
                        Property = nameof(item.Criteria.InstallmentCount),
                        Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_RANGE
                    });
                }

                var criteriaResult = Criteria.FromCodes(item.Criteria.CardBrand, item.Criteria.CardType,
                    item.Criteria.TransactionRegion, item.Criteria.InstallmentCount);
                if (!criteriaResult.IsSuccess)
                    return FeatureObjectResultModel<BulkUpsertBankCommissionsResponse>.Error(criteriaResult.Messages);

                var criteria = criteriaResult.Data!;

                // Aynı istekte daha önce işlenen kriter → onu güncelle (kopya oluşturma).
                if (touched.TryGetValue(criteria, out var pending))
                {
                    var reRate = pending.UpdateRate(item.Rate);
                    if (!reRate.IsSuccess)
                        return FeatureObjectResultModel<BulkUpsertBankCommissionsResponse>.Error(reRate.Messages);
                    continue;
                }

                var match = existing.FirstOrDefault(c => Equals(c.Criteria, criteria));
                if (match is not null)
                {
                    var upd = match.UpdateRate(item.Rate);
                    if (!upd.IsSuccess)
                        return FeatureObjectResultModel<BulkUpsertBankCommissionsResponse>.Error(upd.Messages);

                    session.Update(match);
                    touched[criteria] = match;
                    response.Updated++;
                }
                else
                {
                    var created = BankCommission.Create(cmd.BankCode, criteria, item.Rate);
                    if (!created.IsSuccess)
                        return FeatureObjectResultModel<BulkUpsertBankCommissionsResponse>.Error(created.Messages);

                    session.Store(created.Data!);
                    touched[criteria] = created.Data!;
                    response.Created++;
                }
            }

            return FeatureObjectResultModel<BulkUpsertBankCommissionsResponse>.Ok(response);
        }
    }
}

public static class BulkUpsertBankCommissionsCommandEndpoint
{
    public static RouteGroupBuilder BulkUpsertBankCommissionsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/bulk",
                async ([FromBody] BulkUpsertBankCommissions.BulkUpsertBankCommissionsCommand cmd, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<BulkUpsertBankCommissions.BulkUpsertBankCommissionsResponse>>(cmd);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("BulkUpsertBankCommissions")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.CommissionWrite)
            .Produces<BulkUpsertBankCommissions.BulkUpsertBankCommissionsResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}