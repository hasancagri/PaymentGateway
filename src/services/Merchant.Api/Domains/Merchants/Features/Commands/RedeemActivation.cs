using MerchantAggregate = Merchant.Api.Domains.Merchants.Merchant;

namespace Merchant.Api.Domains.Merchants.Features.Commands;

/// <summary>
/// US3 — aktivasyon bileti kullanımı (Identity aktivasyon sayfasından senkron çağrılır). 015: bilet
/// artık ayrı aggregate değil, <see cref="MerchantAggregate"/>'in alanıdır — merchant
/// <c>ActivationToken</c> ile bulunur, <see cref="MerchantAggregate.RedeemActivation"/> tek-kullanım +
/// TTL invariant'ını uygular (başarıda Provision etkisi). Başarıda <c>MerchantProvisioned</c> yayınlanır
/// (outbox; Identity Provisioning demetiyle client kurar) → MerchantKey yanıtta <b>bir kez</b> döner.
/// İkinci redeem / süre dolmuş → RET (key yeniden gösterilmez, FR-009).
/// </summary>
public static class RedeemActivation
{
    public record RedeemActivationCommand(string ActivationToken);

    public class RedeemActivationResponse
    {
        public Guid MerchantId { get; set; }
        public string MerchantKey { get; set; } = string.Empty;
    }

    [Transactional]
    public class RedeemActivationCommandHandler
    {
        public async Task<FeatureObjectResultModel<RedeemActivationResponse>> Handle(
            RedeemActivationCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var token = cmd.ActivationToken?.Trim() ?? string.Empty;

            // 015: bileti taşıyan merchant'ı aktivasyon token'ıyla bul (eski ActivationTicket sorgusu yerine).
            var merchant = await session.Query<MerchantAggregate>()
                .Where(m => m.ActivationToken == token)
                .FirstOrDefaultAsync(ct);

            if (merchant is null)
                return FeatureObjectResultModel<RedeemActivationResponse>.NotFound();

            var redeem = merchant.RedeemActivation(DateTime.UtcNow);
            if (!redeem.IsSuccess)
                return FeatureObjectResultModel<RedeemActivationResponse>.Error(redeem.Messages);

            session.Update(merchant);

            // Outbox: statü değişikliği + event aynı commit. Identity Provisioning demetiyle client kurar.
            await bus.PublishAsync(new Shared.IntegrationEvents.MerchantProvisioned(
                merchant.Id, merchant.MerchantKey, MerchantStatus.Provisioning.ToString()));

            return FeatureObjectResultModel<RedeemActivationResponse>.Ok(new RedeemActivationResponse
            {
                MerchantId = merchant.Id,
                MerchantKey = merchant.MerchantKey
            });
        }
    }
}

public static class RedeemActivationEndpoint
{
    public static RouteGroupBuilder RedeemActivationGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/activation/redeem",
                async ([FromBody] RedeemActivation.RedeemActivationCommand cmd, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<RedeemActivation.RedeemActivationResponse>>(cmd);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("RedeemActivation")
            .MapToApiVersion(1, 0)
            // Servis-arası (Identity aktivasyon sayfası, claim'siz makine token'ı) — AdminPlaneOnly.
            .RequireAuthorization(AuthorizationScopes.MerchantWrite, AuthorizationPolicies.AdminPlaneOnly)
            .Produces<RedeemActivation.RedeemActivationResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        return group;
    }
}