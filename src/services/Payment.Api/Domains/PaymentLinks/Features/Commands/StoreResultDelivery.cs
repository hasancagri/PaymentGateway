using System.Security.Cryptography;
using System.Text;

namespace Payment.Api.Domains.PaymentLinks.Features.Commands;

/// <summary>
/// 090 (FR-009, İlke VI): Store'a giden HMAC-imzalı dayanıklı sonuç bildirimi (041 StoreCallbackDelivery
/// deseninin yeniden kurulumu). PayPaymentLink handler [Transactional] içinde bus.PublishAsync(Deliver) →
/// yalnız DB commit'te gider (outbox, kayıpsız). Handler ham JSON gövde üretir, X-Signature =
/// HMAC-SHA256(CallbackSecret, raw_body) lowercase hex ile CallbackUrl'e POST eder; non-2xx → throw →
/// Wolverine UseDurableLocalQueues retry. Store aynı ham gövdeyi aynı sırla doğrular. CallbackSecret
/// secret-store'dan; ASLA log/trace/body/dönüş DTO'sunda görünmez. Sınıf adı TEKİL "Handler" (çoğul
/// Wolverine 6.4'te keşfedilmez — CLAUDE.md).
/// </summary>
public static class StoreResultDelivery
{
    public const string SignatureHeader = "X-Signature";

    // İç durable mesaj. Store'a giden ham gövde alanları Handle'da bu değerlerden kurulur.
    public record Deliver(
        string CallbackUrl, string PgPaymentRef, string OrderRef, string Status,
        decimal Amount, string Currency, string BankCode, string? ReasonCode);

    // Wolverine mesaj handler'ı — sınıf adı TEKİL "Handler" ile bitmeli (çoğul keşfedilmez, CLAUDE.md).
    public static class DeliverHandler
    {
        private static readonly HttpClient Client = new();

        public static async Task Handle(Deliver msg, StoreCallbackOptions options, ILogger<Deliver> logger)
        {
            // Ham JSON gövde (contracts/store-webhook.md) — hem imzalanır hem POST edilir. Anahtarlar sabit.
            var body = new
            {
                pgPaymentRef = msg.PgPaymentRef,
                orderRef = msg.OrderRef,
                status = msg.Status,
                amount = msg.Amount,
                currency = msg.Currency,
                bankCode = msg.BankCode,
                reasonCode = msg.ReasonCode
            };
            var rawBody = JsonConvert.SerializeObject(body);
            var signature = ComputeSignature(options.CallbackSecret, rawBody);

            using var content = new StringContent(rawBody, Encoding.UTF8, "application/json");
            content.Headers.Add(SignatureHeader, signature);

            var response = await Client.PostAsync(msg.CallbackUrl, content);
            // 2xx dışı → exception → Wolverine durable retry (FR-009). Sonuç PG'de kalıcı, kaybolmaz.
            response.EnsureSuccessStatusCode();
            logger.LogInformation("Store callback teslim edildi: {PgPaymentRef} → {Status}", msg.PgPaymentRef, msg.Status);
        }

        // HMAC-SHA256(secret, raw_body) → lowercase hex (store aynı biçimde doğrular). Sır loglanmaz.
        private static string ComputeSignature(string secret, string rawBody)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
