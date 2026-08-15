// Domain VO (035) — wire Iyzico.Provider.StoredCards.CardInformation ile aynı adlı; alias.
using DomainCardInformation = Payment.Api.Domains.StoredCards.ValueObjects.CardInformation;

namespace Payment.Api.Domains.StoredCards.Features.Commands;

/// <summary>
/// 032 (Model A): Merchant kartını iyzico Saklı Kart'ına kaydeder → yalnız opak token döner. Gateway
/// PAN SAKLAMAZ; iyzico'ya iletir, dönen cardUserKey+cardToken'ı saklar. CVC sözleşmede yok (FR-002/003).
/// iyzico reddederse kayıt oluşmaz (fail-closed, FR-007). Yanıt PAN/kimlik taşımaz — yalnız opak token
/// (dış sözleşme 031 ile aynı, FR-008).
/// </summary>
public static class TokenizeCard
{
    public record TokenizeCardCommand(Guid MerchantId, string Pan, string Expiry, string HolderName);

    /// <summary>HTTP gövdesi — merchantId route'tan gelir, PAN yalnız burada girer (CVV yok).</summary>
    public record TokenizeCardRequest(string Pan, string Expiry, string HolderName);

    /// <summary>Yanıt: YALNIZ token (iyzico kimlikleri gateway'de kalır, dönmez).</summary>
    public class TokenizeCardResponse
    {
        public string Token { get; set; } = string.Empty;
    }

    [Transactional]
    public class TokenizeCardCommandHandler
    {
        public async Task<FeatureObjectResultModel<TokenizeCardResponse>> Handle(
            TokenizeCardCommand cmd, IDocumentSession session, ProviderOptions providerOptions, CancellationToken ct)
        {
            // Ham kart → domain VO (expiry parse + rakam süzme + doğrulama kapsüllü, 035). Model A: Luhn yok.
            var cardInfoResult = DomainCardInformation.Create(cmd.Pan, cmd.Expiry, cmd.HolderName);
            if (!cardInfoResult.IsSuccess)
                return FeatureObjectResultModel<TokenizeCardResponse>.Error(cardInfoResult.Messages);
            var ci = cardInfoResult.Data!;

            var request = new CreateCardRequest
            {
                Locale = "tr",
                ConversationId = "vault-" + cmd.MerchantId.ToString("N")[..8],
                // per-kart cardUserKey (R2 — gruplama yok). iyzico geçerli e-posta ister:
                // '+' ve '.local' TLD reddedilir → kısa local-part + .com (sandbox'ta doğrulandı).
                Email = $"vault{cmd.MerchantId.ToString("N")[..8]}@dropshop.com",
                ExternalId = Guid.NewGuid().ToString("N"),
                // VO → SDK wire CardInformation (anti-corruption sınır).
                Card = new CardInformation
                {
                    CardAlias = "dropshop-card",
                    CardNumber = ci.CardNumber,
                    ExpireMonth = ci.ExpireMonth,
                    ExpireYear = ci.ExpireYear,
                    CardHolderName = ci.CardHolderName
                }
            };

            Card iyzicoCard;
            try
            {
                iyzicoCard = await Card.Create(request, providerOptions);
            }
            catch
            {
                return FeatureObjectResultModel<TokenizeCardResponse>.Error(new MessageItem
                { Property = nameof(cmd.Pan), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });
            }

            if (iyzicoCard is null || iyzicoCard.Status != "success" ||
                string.IsNullOrWhiteSpace(iyzicoCard.CardUserKey) || string.IsNullOrWhiteSpace(iyzicoCard.CardToken))
            {
                return FeatureObjectResultModel<TokenizeCardResponse>.Error(new MessageItem
                { Property = nameof(cmd.Pan), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });
            }

            var result = StoredCard.Create(
                cmd.MerchantId,
                iyzicoCard.CardUserKey,
                iyzicoCard.CardToken,
                iyzicoCard.BinNumber,
                iyzicoCard.LastFourDigits,
                CardAssociationMapper.Map(iyzicoCard.CardAssociation),
                cmd.Expiry,
                cmd.HolderName);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<TokenizeCardResponse>.Error(result.Messages);

            session.Store(result.Data!);

            return FeatureObjectResultModel<TokenizeCardResponse>.Ok(
                new TokenizeCardResponse { Token = result.Data!.Token });
        }
    }
}

public static class TokenizeCardCommandEndpoint
{
    public static RouteGroupBuilder TokenizeCardGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/",
                async (Guid merchantId, [FromBody] TokenizeCard.TokenizeCardRequest body, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<TokenizeCard.TokenizeCardResponse>>(
                        new TokenizeCard.TokenizeCardCommand(merchantId, body.Pan, body.Expiry, body.HolderName));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("TokenizeCard")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.CardsWrite, AuthorizationPolicies.MerchantScoped)
            .Produces<TokenizeCard.TokenizeCardResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        return group;
    }
}
