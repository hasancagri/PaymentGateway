using Payment.Api.CardVault;
using Payment.Api.Provider;
using Payment.Api.Provider.StoredCards;

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
            // Expiry "MM/yy" → ay + 4-hane yıl (iyzico beklentisi).
            var parts = (cmd.Expiry ?? string.Empty).Split('/');
            if (parts.Length != 2 || parts[0].Trim().Length is < 1 or > 2 || parts[1].Trim().Length != 2)
                return FeatureObjectResultModel<TokenizeCardResponse>.Error(new MessageItem
                { Property = nameof(cmd.Expiry), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT });
            var expireMonth = parts[0].Trim().PadLeft(2, '0');
            var expireYear = "20" + parts[1].Trim();

            var digits = new string((cmd.Pan ?? string.Empty).Where(char.IsDigit).ToArray());

            var request = new CreateCardRequest
            {
                Locale = "tr",
                ConversationId = "vault-" + cmd.MerchantId.ToString("N")[..8],
                // per-kart cardUserKey (R2 — gruplama yok). iyzico geçerli e-posta ister:
                // '+' ve '.local' TLD reddedilir → kısa local-part + .com (sandbox'ta doğrulandı).
                Email = $"vault{cmd.MerchantId.ToString("N")[..8]}@dropshop.com",
                ExternalId = Guid.NewGuid().ToString("N"),
                Card = new CardInformation
                {
                    CardAlias = "dropshop-card",
                    CardNumber = digits,
                    ExpireMonth = expireMonth,
                    ExpireYear = expireYear,
                    CardHolderName = (cmd.HolderName ?? string.Empty).Trim()
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
