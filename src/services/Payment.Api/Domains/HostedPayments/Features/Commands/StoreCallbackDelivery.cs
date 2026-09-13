using System.Security.Cryptography;
using System.Text;
using Payment.Api.Options;

namespace Payment.Api.Domains.HostedPayments.Features.Commands;

/// <summary>
/// 041 US2/US3: Store'a giden imzalı sonuç bildirimi. Terminal geçişte CompleteHostedPayment
/// [Transactional] outbox ile publish eder (yalnız DB commit'te gider — FR-009 kayıpsız). Handler ham
/// JSON gövde üretir, X-Signature = HMAC-SHA256(CallbackSecret, raw_body) ile store CallbackUrl'ine POST
/// eder; hata fırlatırsa Wolverine durable retry (kayıpsız yeniden gönderim). CallbackSecret MerchantKey'den
/// AYRI (v1 paylaşılan config). Sınıf adı TEKİL "Handler" — çoğul Wolverine 6.4'te keşfedilmez (CLAUDE.md).
/// </summary>
public static class StoreCallbackDelivery
{
    public const string SignatureHeader = "X-Signature";

    // Store'a giden imzalı gövde alanları (contracts/pg-external.md §2). Serialize edilen ham JSON
    // hem imzalanır hem POST edilir (store aynı ham body'yi CallbackSecret'la doğrular).
    public record Deliver(string CallbackUrl, string TxRef, string PgPaymentRef, string Status, string? ReasonCode);

    // Wolverine mesaj handler'ı — sınıf adı TEKİL "Handler" ile bitmeli (çoğul keşfedilmez, CLAUDE.md).
    public static class DeliverHandler
    {
        private static readonly HttpClient Client = new();

        public static async Task Handle(Deliver msg, HostedPaymentOptions options, ILogger<Deliver> logger)
        {
            var body = new { TxRef = msg.TxRef, PgPaymentRef = msg.PgPaymentRef, Status = msg.Status, ReasonCode = msg.ReasonCode };
            var rawBody = JsonConvert.SerializeObject(body);
            var signature = ComputeSignature(options.CallbackSecret, rawBody);

            using var content = new StringContent(rawBody, Encoding.UTF8, "application/json");
            content.Headers.Add(SignatureHeader, signature);

            var response = await Client.PostAsync(msg.CallbackUrl, content);
            // 2xx dışı → exception fırlat → Wolverine durable retry (FR-009). Sonuç PG'de kalıcı, kaybolmaz.
            response.EnsureSuccessStatusCode();
            logger.LogInformation("Store callback teslim edildi: {PgPaymentRef} → {Status}", msg.PgPaymentRef, msg.Status);
        }

        // HMAC-SHA256(secret, raw_body) → lowercase hex (store aynı biçimde doğrular).
        private static string ComputeSignature(string secret, string rawBody)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
