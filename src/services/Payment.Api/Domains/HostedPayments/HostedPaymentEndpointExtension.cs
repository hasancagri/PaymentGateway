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

                var succeeded = s.Status == HostedPaymentStatus.Succeeded;
                var text = succeeded ? options.ReturnSuccessText : options.ReturnFailureText;
                return Results.Content(BuildReturnPage(succeeded, text), "text/html");
            })
            .WithName("HostedPaymentReturn")
            .WithTags("hosted-payment")
            .AllowAnonymous();
    }

    // Müşteri dönüş sayfası (FR-010) — kendine-yeten stilli HTML. Hiçbir yere yönlendirmez (terminal
    // sayfa); "Kapat" butonu window.close() dener. Kullanıcı metni options'tan (ReturnSuccess/FailureText).
    private static string BuildReturnPage(bool succeeded, string text)
    {
        var accent = succeeded ? "#16a34a" : "#dc2626";
        var glyph = succeeded ? "&#10003;" : "&#10005;"; // ✓ / ✕
        var heading = succeeded ? "Ödeme Başarılı" : "Ödeme Başarısız";
        var body = System.Net.WebUtility.HtmlEncode(text);

        return $$"""
        <!doctype html>
        <html lang="tr">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>Ödeme Sonucu</title>
          <style>
            *{box-sizing:border-box}
            body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;
              background:#f4f5f7;font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Helvetica,Arial,sans-serif;
              color:#1f2937;padding:24px}
            .card{background:#fff;border-radius:16px;box-shadow:0 10px 30px rgba(0,0,0,.08);
              padding:40px 32px;max-width:420px;width:100%;text-align:center}
            .icon{width:72px;height:72px;border-radius:50%;margin:0 auto 20px;
              display:flex;align-items:center;justify-content:center;
              background:{{accent}};color:#fff;font-size:38px;line-height:1}
            h1{margin:0 0 10px;font-size:22px;font-weight:600;color:{{accent}}}
            p{margin:0 0 28px;font-size:15px;line-height:1.5;color:#4b5563}
            button{appearance:none;border:0;cursor:pointer;font-size:15px;font-weight:600;
              padding:12px 28px;border-radius:10px;background:#111827;color:#fff;transition:background .15s}
            button:hover{background:#374151}
            .hint{margin-top:16px;font-size:12px;color:#9ca3af;display:none}
          </style>
        </head>
        <body>
          <div class="card">
            <div class="icon">{{glyph}}</div>
            <h1>{{heading}}</h1>
            <p>{{body}}</p>
            <button onclick="closePage()">Kapat</button>
            <div class="hint" id="hint">Bu sekmeyi kapatabilirsiniz.</div>
          </div>
          <script>
            function closePage(){
              window.close();
              document.getElementById('hint').style.display='block';
            }
          </script>
        </body>
        </html>
        """;
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
