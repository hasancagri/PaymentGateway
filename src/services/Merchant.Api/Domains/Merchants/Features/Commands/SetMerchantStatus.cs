namespace Merchant.Api.Domains.Merchants.Features.Commands;

// 012: merchant yaşam döngüsü — admin düzlemi ucu (AdminPlaneOnly: merchant kendi statüsünü
// değiştiremez, kendini askıdan çıkaramaz). Başarıda MerchantStatusChanged yayınlanır;
// Identity.Server tüketip token vermeyi statüye göre açar/kapar (status-gated issuance).
public static class SetMerchantStatus
{
    public record SetMerchantStatusCommand(Guid MerchantId, string Status);

    public class SetMerchantStatusResponse
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    [Transactional]
    public class SetMerchantStatusCommandHandler
    {
        public async Task<FeatureObjectResultModel<SetMerchantStatusResponse>> Handle(
            SetMerchantStatusCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            if (!Enum.TryParse<MerchantStatus>(cmd.Status?.Trim(), ignoreCase: true, out var target))
                return FeatureObjectResultModel<SetMerchantStatusResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Status),
                    Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
                });

            var merchant = await session.LoadAsync<Merchant>(cmd.MerchantId, ct);
            if (merchant is null)
                return FeatureObjectResultModel<SetMerchantStatusResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.MerchantId),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            switch (target)
            {
                case MerchantStatus.Active:
                    var activate = merchant.Activate();
                    if (!activate.IsSuccess)
                        return FeatureObjectResultModel<SetMerchantStatusResponse>.Error(activate.Messages);
                    break;
                case MerchantStatus.Passive:
                    merchant.Deactivate();
                    break;
                case MerchantStatus.Suspended:
                    merchant.Suspend();
                    break;
            }

            session.Store(merchant);

            // Identity.Server tüketir → OpenIddict istemci izinleri statüye göre açılır/kapanır (D4).
            await bus.PublishAsync(new Shared.IntegrationEvents.MerchantStatusChanged(
                merchant.Id, merchant.Status.ToString()));

            return FeatureObjectResultModel<SetMerchantStatusResponse>.Ok(new SetMerchantStatusResponse
            {
                Id = merchant.Id,
                Status = merchant.Status.ToString()
            });
        }
    }
}

public static class SetMerchantStatusEndpoint
{
    public record SetMerchantStatusBody(string Status);

    public static RouteGroupBuilder SetMerchantStatusGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/{merchantId:guid}/status",
                async (Guid merchantId, [FromBody] SetMerchantStatusBody body, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<SetMerchantStatus.SetMerchantStatusResponse>>(
                            new SetMerchantStatus.SetMerchantStatusCommand(merchantId, body.Status));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
                })
            .WithName("SetMerchantStatus")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantWrite, AuthorizationPolicies.AdminPlaneOnly)
            .Produces<SetMerchantStatus.SetMerchantStatusResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}