using Payment.Api.CardVault;

namespace Payment.Api.Domains.StoredCards.Features.Commands;

/// <summary>
/// Merchant kartını tokenize eder → yalnız opak token döner. PAN normalize + Luhn + expiry
/// doğrulanır, korunmuş saklanır (enc-at-rest); yanıt PAN/last4/brand/bin TAŞIMAZ (PAN sınırı
/// geçmez — FR-001/FR-004). CVV sözleşmede yok (FR-002). iyzico çağrısı yok (FR-010).
/// </summary>
public static class TokenizeCard
{
    public record TokenizeCardCommand(Guid MerchantId, string Pan, string Expiry, string HolderName);

    /// <summary>HTTP gövdesi — merchantId route'tan gelir, PAN yalnız burada girer (CVV yok).</summary>
    public record TokenizeCardRequest(string Pan, string Expiry, string HolderName);

    /// <summary>Yanıt: YALNIZ token (PAN/last4/brand/bin dönmez).</summary>
    public class TokenizeCardResponse
    {
        public string Token { get; set; } = string.Empty;
    }

    [Transactional]
    public class TokenizeCardCommandHandler
    {
        public async Task<FeatureObjectResultModel<TokenizeCardResponse>> Handle(
            TokenizeCardCommand cmd, IDocumentSession session, IPanProtector protector, CancellationToken ct)
        {
            var result = StoredCard.Create(cmd.MerchantId, cmd.Pan, cmd.Expiry, cmd.HolderName, protector);
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
