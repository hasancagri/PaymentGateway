using System.Security.Claims;
using Common.Utils.Authorization;
using Payment.Api.Domains.StoredCards.Features.Commands;
using Payment.Api.Domains.StoredCards.Features.Queries;

namespace Payment.Api.Domains.StoredCards;

/// <summary>
/// 040: Store 075 hosted kart-saklama yüzeyi — merchant-less <c>/vault/card-sessions</c> + <c>/vault/cards</c>
/// (store PgCardClient bu yolları çağırır). Auth: OpenIddict <c>cards.write</c> scope + <c>merchant_id</c>
/// claim (İLKE V claim-tabanlı tenant; merchant route'ta DEĞİL, token'dan çözülür). Charge ucu ayrı
/// (merchants/{merchantId}/payments, X-Api-Key — 039).
/// </summary>
public static class CardVaultEndpointExtension
{
    public static void AddCardVaultEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        // --- /vault/card-sessions (hosted kart-ekleme) ---
        var sessions = app.MapGroup("api/v{version:apiVersion}/vault/card-sessions")
            .WithTags("card-vault")
            .WithApiVersionSet(apiVersionSet);

        sessions.MapPost("/", async (
                [FromBody] StartSessionBody body, ClaimsPrincipal user, IMessageBus bus) =>
            {
                if (!TryMerchant(user, out var merchantId)) return Results.Unauthorized();
                if (!Guid.TryParse(body.ConversationId, out var conversationId)) return Results.BadRequest();

                var result = await bus.InvokeAsync<FeatureObjectResultModel<StartCardSession.StartCardSessionResponse>>(
                    new StartCardSession.StartCardSessionCommand(merchantId, conversationId, body.CallbackUrl, body.UserHandle));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("StartCardSession")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.CardsWrite);

        sessions.MapGet("/{conversationId:guid}", async (
                Guid conversationId, ClaimsPrincipal user, IMessageBus bus) =>
            {
                if (!TryMerchant(user, out var merchantId)) return Results.Unauthorized();

                var result = await bus.InvokeAsync<FeatureObjectResultModel<CompleteCardSession.CompleteCardSessionResponse>>(
                    new CompleteCardSession.CompleteCardSessionCommand(merchantId, conversationId));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CompleteCardSession")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.CardsWrite);

        // --- /vault/cards (liste + sil) ---
        var cards = app.MapGroup("api/v{version:apiVersion}/vault/cards")
            .WithTags("card-vault")
            .WithApiVersionSet(apiVersionSet);

        cards.MapGet("/", async (
                string userHandle, ClaimsPrincipal user, IMessageBus bus) =>
            {
                if (!TryMerchant(user, out var merchantId)) return Results.Unauthorized();

                var result = await bus.InvokeAsync<FeatureListResultModel<ListCards.CardView>>(
                    new ListCards.ListCardsQuery(merchantId, userHandle));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("ListCards")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.CardsWrite);

        cards.MapDelete("/", async (
                [FromBody] DeleteCardBody body, ClaimsPrincipal user, IMessageBus bus) =>
            {
                if (!TryMerchant(user, out var merchantId)) return Results.Unauthorized();

                var result = await bus.InvokeAsync<FeatureObjectResultModel<DeleteCard.DeleteCardResponse>>(
                    new DeleteCard.DeleteCardCommand(merchantId, body.UserHandle, body.CardHandle));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("DeleteCard")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.CardsWrite);
    }

    // merchant_id claim → Guid (İLKE V tenant). Claim yoksa/geçersizse fail-closed.
    private static bool TryMerchant(ClaimsPrincipal user, out Guid merchantId)
    {
        merchantId = Guid.Empty;
        var raw = user.FindFirst(MerchantScopeAuthorizationHandler.MerchantIdClaim)?.Value;
        return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out merchantId);
    }

    // Store PgCardClient gövdeleri (pg-card-contract.md).
    public record StartSessionBody(Guid MerchantId, string ConversationId, string CallbackUrl, string? UserHandle);
    public record DeleteCardBody(string UserHandle, string CardHandle);
}
