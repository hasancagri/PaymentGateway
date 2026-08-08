using MerchantAggregate = Merchant.Api.Domains.Merchants.Merchant;

namespace Merchant.Api.Domains.Merchants.Features.Commands;

/// <summary>
/// US5 — merchant kendi token'ıyla ReturnUrl set/update eder (HTTPS zorunlu; Active koşulu #3).
/// Ardından <c>TryActivate</c>: 3 koşul dolmuşsa Provisioning→Active + <c>MerchantStatusChanged(Active)</c>
/// (outbox). MerchantScoped (merchant_id claim = route {merchantId}).
/// </summary>
public static class SetReturnUrl
{
    public record SetReturnUrlCommand(Guid MerchantId, string ReturnUrl);

    public class SetReturnUrlResponse
    {
        public Guid MerchantId { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    [Transactional]
    public class SetReturnUrlCommandHandler
    {
        public async Task<FeatureObjectResultModel<SetReturnUrlResponse>> Handle(
            SetReturnUrlCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<MerchantAggregate>(cmd.MerchantId, ct);
            if (merchant is null)
                return FeatureObjectResultModel<SetReturnUrlResponse>.NotFound();

            var set = merchant.SetReturnUrl(cmd.ReturnUrl);
            if (!set.IsSuccess)
                return FeatureObjectResultModel<SetReturnUrlResponse>.Error(set.Messages);

            var activated = merchant.TryActivate();
            session.Update(merchant);

            if (activated)
                await bus.PublishAsync(new Shared.IntegrationEvents.MerchantStatusChanged(
                    merchant.Id, MerchantStatus.Active.ToString()));

            return FeatureObjectResultModel<SetReturnUrlResponse>.Ok(new SetReturnUrlResponse
            {
                MerchantId = merchant.Id,
                Status = merchant.Status.ToString()
            });
        }
    }
}

public static class SetReturnUrlEndpoint
{
    public static RouteGroupBuilder SetReturnUrlGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/{merchantId:guid}/return-url",
                async (Guid merchantId, [FromBody] SetReturnUrlBody body, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<SetReturnUrl.SetReturnUrlResponse>>(
                        new SetReturnUrl.SetReturnUrlCommand(merchantId, body.ReturnUrl));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("SetReturnUrl")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantWrite, AuthorizationPolicies.MerchantScoped)
            .Produces<SetReturnUrl.SetReturnUrlResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        return group;
    }

    public record SetReturnUrlBody(string ReturnUrl);
}
