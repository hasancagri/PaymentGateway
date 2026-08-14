namespace Payment.Api.Domains.StoredCards.Features.Commands;

/// <summary>
/// Merchant kartını soft iptal eder (Status=Revoked; fiziksel durur). Idempotent. Sahiplik: kart
/// route <c>{merchantId}</c>'ye ait olmalı (aksi RECORD_NOT_FOUND — sahiplik sızdırmaz) —
/// MerchantScoped'ın üstüne ikinci kapı.
/// </summary>
public static class RevokeCard
{
    public record RevokeCardCommand(Guid MerchantId, string Token);

    public class RevokeCardResponse
    {
        public string Token { get; set; } = string.Empty;
    }

    [Transactional]
    public class RevokeCardCommandHandler
    {
        public async Task<FeatureObjectResultModel<RevokeCardResponse>> Handle(
            RevokeCardCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var card = await session.LoadAsync<StoredCard>(cmd.Token, ct);
            if (card is null || card.MerchantId != cmd.MerchantId)
            {
                return FeatureObjectResultModel<RevokeCardResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Token),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });
            }

            var result = card.Revoke();
            if (!result.IsSuccess)
                return FeatureObjectResultModel<RevokeCardResponse>.Error(result.Messages);

            session.Update(card);

            return FeatureObjectResultModel<RevokeCardResponse>.Ok(new RevokeCardResponse { Token = card.Token });
        }
    }
}

public static class RevokeCardCommandEndpoint
{
    public static RouteGroupBuilder RevokeCardGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapDelete("/{token}",
                async (Guid merchantId, string token, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<RevokeCard.RevokeCardResponse>>(
                        new RevokeCard.RevokeCardCommand(merchantId, token));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("RevokeCard")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.CardsWrite, AuthorizationPolicies.MerchantScoped)
            .Produces<RevokeCard.RevokeCardResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        return group;
    }
}
