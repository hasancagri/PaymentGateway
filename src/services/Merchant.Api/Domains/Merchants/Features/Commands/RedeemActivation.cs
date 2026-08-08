using MerchantAggregate = Merchant.Api.Domains.Merchants.Merchant;

namespace Merchant.Api.Domains.Merchants.Features.Commands;

/// <summary>
/// US3 — aktivasyon bileti kullanımı (Identity aktivasyon sayfasından senkron çağrılır). Bilet
/// doğrulanır (tek-kullanım, TTL) → <see cref="MerchantAggregate.Provision"/> → <c>MerchantProvisioned</c>
/// yayınlanır (outbox; Identity Provisioning demetiyle client kurar) → MerchantKey yanıtta <b>bir kez</b>
/// döner. İkinci redeem / süre dolmuş → RET (key yeniden gösterilmez, FR-009).
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
            var ticket = await session.Query<ActivationTicket>()
                .Where(t => t.Token == token)
                .FirstOrDefaultAsync(ct);

            if (ticket is null)
                return FeatureObjectResultModel<RedeemActivationResponse>.NotFound();

            var redeem = ticket.Redeem(DateTime.UtcNow);
            if (!redeem.IsSuccess)
            {
                session.Update(ticket); // süre dolmuşsa Expired'e geçişi kalıcılaştır
                return FeatureObjectResultModel<RedeemActivationResponse>.Error(redeem.Messages);
            }

            var merchant = await session.LoadAsync<MerchantAggregate>(ticket.MerchantId, ct);
            if (merchant is null)
                return FeatureObjectResultModel<RedeemActivationResponse>.NotFound();

            merchant.Provision();

            session.Update(ticket);
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
