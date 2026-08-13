namespace Merchant.Api.Domains.Merchants.Features.Commands;

// 023: statü yönetimi — yalnız admin düzlemi (AdminPlaneOnly: merchant kendini askıdan çıkaramaz).
// Gerçek değişiklikte MerchantStatusChanged yayınlanır ([Transactional] outbox — commit'le atomik);
// aynı statüye geçiş idempotent no-op, yayın YOK (R5). Identity.Server tüketip token vermeyi
// statüye göre açar/kapar (yalnız Active token alır).
public static class ChangeMerchantStatus
{
    public record ChangeMerchantStatusCommand(Guid MerchantId, string Status);

    public class ChangeMerchantStatusResponse
    {
        public Guid MerchantId { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    [Transactional]
    public class ChangeMerchantStatusCommandHandler
    {
        public async Task<FeatureObjectResultModel<ChangeMerchantStatusResponse>> Handle(
            ChangeMerchantStatusCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            if (!Enum.TryParse<MerchantStatus>(cmd.Status?.Trim(), ignoreCase: true, out var target))
                return FeatureObjectResultModel<ChangeMerchantStatusResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Status),
                    Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
                });

            var merchant = await session.LoadAsync<Merchant>(cmd.MerchantId, ct);
            if (merchant is null || merchant.IsDeleted)
                return FeatureObjectResultModel<ChangeMerchantStatusResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.MerchantId),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var changed = merchant.ChangeStatus(target);
            if (changed.Data!)
            {
                session.Store(merchant);

                // Identity.Server tüketir → OpenIddict istemci izinleri statüye göre açılır/kapanır.
                await bus.PublishAsync(new Shared.IntegrationEvents.MerchantStatusChanged(
                    merchant.Id, merchant.Status.ToString()));
            }

            return FeatureObjectResultModel<ChangeMerchantStatusResponse>.Ok(new ChangeMerchantStatusResponse
            {
                MerchantId = merchant.Id,
                Status = merchant.Status.ToString()
            });
        }
    }
}

public static class ChangeMerchantStatusEndpoint
{
    /// <summary>Gövde modeli; <c>merchantId</c> rotadan gelir.</summary>
    public record ChangeMerchantStatusBody(string Status);

    public static RouteGroupBuilder ChangeMerchantStatusGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/{merchantId:guid}/status",
                async (Guid merchantId, [FromBody] ChangeMerchantStatusBody body, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<ChangeMerchantStatus.ChangeMerchantStatusResponse>>(
                            new ChangeMerchantStatus.ChangeMerchantStatusCommand(merchantId, body.Status));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("ChangeMerchantStatus")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantWrite, AuthorizationPolicies.AdminPlaneOnly)
            .Produces<ChangeMerchantStatus.ChangeMerchantStatusResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}