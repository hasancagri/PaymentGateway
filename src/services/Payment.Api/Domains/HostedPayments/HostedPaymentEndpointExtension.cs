using System.Security.Claims;
using Common.Utils.Authorization;
using Payment.Api.Domains.HostedPayments.Features.Commands;
using Payment.Api.Options;

namespace Payment.Api.Domains.HostedPayments;

/// <summary>
/// 041: hosted-CF ödeme yüzeyi uçları. Sürüm segmentsiz (dış store kontratı sabit yol bekler — D11).
/// (1) POST /hosted-payment — store başlatma (X-Api-Key, merchant_id claim'den tenant). (2) POST
/// /internal/payments/callback/{callbackToken} — iyzico dönüşü (C1 secret-token kapılı). (3) GET
/// /payments/return/{pgPaymentRef} — müşteri dönüş sayfası.
/// </summary>
public static class HostedPaymentEndpointExtension
{
    public static void AddHostedPaymentEndpointExtension(this WebApplication app)
    {
        // (1) Store → PG: hosted ödeme başlat (X-Api-Key).
        app.MapPost("/hosted-payment", async (
                [FromBody] HostedPaymentBody body, ClaimsPrincipal user, IMessageBus bus) =>
            {
                if (!TryMerchant(user, out var merchantId)) return Results.Unauthorized();

                var result = await bus.InvokeAsync<FeatureObjectResultModel<InitiateHostedPayment.InitiateHostedPaymentResponse>>(
                    new InitiateHostedPayment.InitiateHostedPaymentCommand(
                        merchantId, body.Amount, body.Currency, body.OrderRef, body.CallbackUrl));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("InitiateHostedPayment")
            .WithTags("hosted-payment")
            .RequireAuthorization(HostedPaymentPolicies.HostedPaymentApiKey);

        // (2) iyzico → PG: hosted form dönüşü (secret-token kapılı, kimliksiz). Form-body: token.
        app.MapPost("/internal/payments/callback/{callbackToken}", async (
                string callbackToken, HttpRequest request, IMessageBus bus, HostedPaymentOptions options) =>
            {
                var form = await request.ReadFormAsync();
                var iyzicoToken = form["token"].ToString();

                var result = await bus.InvokeAsync<FeatureObjectResultModel<CompleteHostedPayment.CompleteHostedPaymentResponse>>(
                    new CompleteHostedPayment.CompleteHostedPaymentCommand(callbackToken, iyzicoToken));

                // Bilinmeyen token / işlenemeyen dönüş → 404 (durum değişmez, store'a bildirim yok).
                if (!result.IsSuccess || result.Data is null)
                    return Results.NotFound();

                // Müşteri tarayıcısını sonuca uygun dönüş sayfasına yönlendir (FR-010).
                return Results.Redirect($"{options.ReturnPageBaseUrl}/{result.Data.PgPaymentRef}");
            })
            .WithName("HostedPaymentCallback")
            .WithTags("hosted-payment")
            .AllowAnonymous();

        // (3) PG → Müşteri: dönüş sayfası (FR-010). Hassas veri yok — yalnız durum metni.
        app.MapGet("/payments/return/{pgPaymentRef}", async (
                string pgPaymentRef, IQuerySession session, HostedPaymentOptions options) =>
            {
                if (!Guid.TryParse(pgPaymentRef, out var id))
                    return Results.NotFound();

                var s = await session.LoadAsync<HostedPaymentSession>(id);
                if (s is null) return Results.NotFound();

                var text = s.Status == HostedPaymentStatus.Succeeded ? options.ReturnSuccessText : options.ReturnFailureText;
                var html = $"<!doctype html><html lang=\"tr\"><head><meta charset=\"utf-8\"><title>Ödeme Sonucu</title></head>" +
                           $"<body><p>{System.Net.WebUtility.HtmlEncode(text)}</p></body></html>";
                return Results.Content(html, "text/html");
            })
            .WithName("HostedPaymentReturn")
            .WithTags("hosted-payment")
            .AllowAnonymous();
    }

    // merchant_id claim → Guid (İlke V tenant). Claim yoksa/geçersizse fail-closed.
    private static bool TryMerchant(ClaimsPrincipal user, out Guid merchantId)
    {
        merchantId = Guid.Empty;
        var raw = user.FindFirst(MerchantScopeAuthorizationHandler.MerchantIdClaim)?.Value;
        return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out merchantId);
    }

    // Store hosted ödeme başlatma gövdesi (contracts/pg-external.md §1).
    public record HostedPaymentBody(decimal Amount, string Currency, string OrderRef, string CallbackUrl);
}
