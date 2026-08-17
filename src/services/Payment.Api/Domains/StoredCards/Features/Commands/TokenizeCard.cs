using Iyz = Payment.Api.Utils;
using Payment.Api.Options;
// Domain VO (035) — wire card ile aynı kavram; handler VO'dan wire'a map'ler (anti-corruption sınır).
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

    /// <summary>iyzico "Saklı Kart oluştur" istek gövdesi (wire) — bu slice'a ait. camelCase JSON, base tip yok.</summary>
    public class CreateCardRequest
    {
        public string Locale { get; set; } = string.Empty;
        public string ConversationId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ExternalId { get; set; } = string.Empty;
        public CardInfo Card { get; set; } = new();
    }

    /// <summary>iyzico wire kart bilgisi (istek). Domain VO değil — serileşme tipi.</summary>
    public class CardInfo
    {
        public string CardAlias { get; set; } = string.Empty;
        public string CardNumber { get; set; } = string.Empty;
        public string ExpireYear { get; set; } = string.Empty;
        public string ExpireMonth { get; set; } = string.Empty;
        public string CardHolderName { get; set; } = string.Empty;
    }

    /// <summary>iyzico Saklı Kart yanıtı (wire) — Status/Error alanları Iyz.ProviderResourceV2'den.</summary>
    public class CardResult : Iyz.ProviderResourceV2
    {
        public string CardUserKey { get; set; } = string.Empty;
        public string CardToken { get; set; } = string.Empty;
        public string BinNumber { get; set; } = string.Empty;
        public string LastFourDigits { get; set; } = string.Empty;
        public string CardAssociation { get; set; } = string.Empty;
    }

    [Transactional]
    public class TokenizeCardCommandHandler
    {
        public async Task<FeatureObjectResultModel<TokenizeCardResponse>> Handle(
            TokenizeCardCommand cmd, IDocumentSession session, Iyz.ProviderOptions providerOptions,
            IyzicoRequestOptions requestOptions, CancellationToken ct)
        {
            // Ham kart → domain VO (expiry parse + rakam süzme + doğrulama kapsüllü, 035). Model A: Luhn yok.
            var cardInfoResult = DomainCardInformation.Create(cmd.Pan, cmd.Expiry, cmd.HolderName);
            if (!cardInfoResult.IsSuccess)
                return FeatureObjectResultModel<TokenizeCardResponse>.Error(cardInfoResult.Messages);
            var ci = cardInfoResult.Data!;

            var request = new CreateCardRequest
            {
                Locale = requestOptions.Locale,
                ConversationId = requestOptions.ConversationId,
                // per-kart cardUserKey (R2 — gruplama yok). iyzico geçerli e-posta ister:
                // '+' ve '.local' TLD reddedilir → kısa local-part + config domaini (sandbox'ta doğrulandı).
                Email = $"{requestOptions.EmailLocalPrefix}{cmd.MerchantId.ToString("N")[..8]}@{requestOptions.EmailDomain}",
                ExternalId = Guid.NewGuid().ToString("N"),
                // VO → wire CardInfo (anti-corruption sınır).
                Card = new CardInfo
                {
                    CardAlias = requestOptions.CardAlias,
                    CardNumber = ci.CardNumber,
                    ExpireMonth = ci.ExpireMonth,
                    ExpireYear = ci.ExpireYear,
                    CardHolderName = ci.CardHolderName
                }
            };

            CardResult iyzicoCard;
            try
            {
                var uri = providerOptions.BaseUrl + requestOptions.CardStoragePath;
                var headers = Iyz.ProviderResourceV2.GetHttpHeadersWithRequestBody(request, uri, providerOptions, request.ConversationId);
                iyzicoCard = await Iyz.RestHttpClientV2.Create().PostAsync<CardResult>(uri, headers, request);
            }
            catch
            {
                return FeatureObjectResultModel<TokenizeCardResponse>.Error(new MessageItem
                { Property = nameof(cmd.Pan), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });
            }

            if (iyzicoCard is null || iyzicoCard.Status != requestOptions.SuccessStatus ||
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