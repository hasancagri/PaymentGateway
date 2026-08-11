using MerchantAggregate = Merchant.Api.Domains.Merchants.Merchant;

namespace Merchant.Api.Domains.Merchants.Features.Commands;

/// <summary>
/// Admin "Finalize" (A modeli): koşullar tamamsa Provisioning→Active elle tetiklenir. Otomatik geçiş
/// (settlement/grid/ReturnUrl handler'larındaki <c>TryActivate</c>) aynen kalır — bu uç kaçan event
/// telafisi + admin görünürlüğü. Koşul setini <c>TryActivate</c> AYNEN uygular; koşulsuz geçiş yok
/// (acil kapı <c>SetMerchantStatus</c>). Geçiş olmasa da Ok döner — yanıt bayrakları hangi koşulun
/// eksik olduğunu söyler (UI çeklisti).
/// </summary>
public static class FinalizeMerchant
{
    public record FinalizeMerchantCommand(Guid MerchantId);

    public class FinalizeMerchantResponse
    {
        public Guid MerchantId { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool Activated { get; set; }
        public bool HasSettlementAccount { get; set; }
        public bool CommissionGridReady { get; set; }
        public bool HasReturnUrl { get; set; }
    }

    [Transactional]
    public class FinalizeMerchantCommandHandler
    {
        public async Task<FeatureObjectResultModel<FinalizeMerchantResponse>> Handle(
            FinalizeMerchantCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<MerchantAggregate>(cmd.MerchantId, ct);
            if (merchant is null)
                return FeatureObjectResultModel<FinalizeMerchantResponse>.NotFound();

            var activated = merchant.TryActivate().IsSuccess;
            if (activated)
            {
                session.Update(merchant);
                await bus.PublishAsync(new Shared.IntegrationEvents.MerchantStatusChanged(
                    merchant.Id, MerchantStatus.Active.ToString()));
            }

            return FeatureObjectResultModel<FinalizeMerchantResponse>.Ok(new FinalizeMerchantResponse
            {
                MerchantId = merchant.Id,
                Status = merchant.Status.ToString(),
                Activated = activated,
                HasSettlementAccount = merchant.HasSettlementAccount,
                CommissionGridReady = merchant.CommissionGridReady,
                HasReturnUrl = !string.IsNullOrWhiteSpace(merchant.ReturnUrl)
            });
        }
    }
}

public static class FinalizeMerchantEndpoint
{
    public static RouteGroupBuilder FinalizeMerchantGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{merchantId:guid}/finalize",
                async (Guid merchantId, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<FinalizeMerchant.FinalizeMerchantResponse>>(
                        new FinalizeMerchant.FinalizeMerchantCommand(merchantId));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
                })
            .WithName("FinalizeMerchant")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantWrite, AuthorizationPolicies.AdminPlaneOnly)
            .Produces<FinalizeMerchant.FinalizeMerchantResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        return group;
    }
}