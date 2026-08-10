using Common.Utils.Constants;

namespace Payment.Api.Domains.StoredCards.Features.Commands;

/// <summary>
/// Merchant kartın expiry + sahibini günceller. PAN YOK → token/bin/last4/brand sabit. Revoked kart
/// RET. Sahiplik: kart route <c>{merchantId}</c>'ye ait olmalı.
/// </summary>
public static class UpdateCard
{
    public record UpdateCardCommand(Guid MerchantId, string Token, string Expiry, string HolderName);

    /// <summary>HTTP gövdesi — PAN alanı KABUL EDİLMEZ (immutable PAN kararı).</summary>
    public record UpdateCardRequest(string Expiry, string HolderName);

    public class UpdateCardResponse
    {
        public string Token { get; set; } = string.Empty;
    }

    [Transactional]
    public class UpdateCardCommandHandler
    {
        public async Task<FeatureObjectResultModel<UpdateCardResponse>> Handle(
            UpdateCardCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var card = await session.LoadAsync<StoredCard>(cmd.Token, ct);
            if (card is null || card.MerchantId != cmd.MerchantId)
            {
                return FeatureObjectResultModel<UpdateCardResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Token),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });
            }

            var result = card.UpdateDetails(cmd.Expiry, cmd.HolderName);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<UpdateCardResponse>.Error(result.Messages);

            session.Update(card);

            return FeatureObjectResultModel<UpdateCardResponse>.Ok(new UpdateCardResponse { Token = card.Token });
        }
    }
}

public static class UpdateCardCommandEndpoint
{
    public static RouteGroupBuilder UpdateCardGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/{token}",
                async (Guid merchantId, string token, [FromBody] UpdateCard.UpdateCardRequest body, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<UpdateCard.UpdateCardResponse>>(
                        new UpdateCard.UpdateCardCommand(merchantId, token, body.Expiry, body.HolderName));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("UpdateCard")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.CardsWrite, AuthorizationPolicies.MerchantScoped)
            .Produces<UpdateCard.UpdateCardResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        return group;
    }
}
