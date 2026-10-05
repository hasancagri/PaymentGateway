using System.Security.Cryptography;
using System.Text;
using Merchant.Api.Options;

namespace Merchant.Api.Domains.Merchants.Features.Commands;

/// <summary>
/// 087 US1: store'a giden imzalı credential teslimi (makine-handoff; insan-yüzeyi YOK). Onay
/// (AdminApproveRegistration) ve reissue (ReissueMerchantKey) [Transactional] outbox ile publish
/// eder (yalnız DB commit'te gider — kayıpsız). Handler ham JSON gövde üretir, X-Signature =
/// HMAC-SHA256(CallbackSecret, raw_body) ile store CallbackUrl'ine POST eder; 2xx dışı → exception
/// → Wolverine durable retry (kayıpsız yeniden gönderim). 041 StoreCallbackDelivery aynası.
/// CallbackSecret MerchantKey'den AYRI; MerchantKey ASLA log/trace'e yazılmaz (yalnız imzalı gövde).
/// Sınıf adı TEKİL "Handler" — çoğul Wolverine 6.4'te keşfedilmez (CLAUDE.md).
/// </summary>
public static class DeliverCredentialCallback
{
    public const string SignatureHeader = "X-Signature";

    // Store'a giden imzalı gövde (contracts/credential-callback.md). Serialize edilen ham JSON hem
    // imzalanır hem POST edilir (store aynı ham body'yi CallbackSecret'la doğrular). CorrelationId =
    // register/reissue'daki ile eşleşir (store idempotency).
    public record Deliver(string CallbackUrl, Guid CorrelationId, Guid MerchantId, string MerchantKey, string Status);

    // Wolverine mesaj handler'ı — sınıf adı TEKİL "Handler" ile bitmeli (çoğul keşfedilmez, CLAUDE.md).
    public static class DeliverHandler
    {
        private static readonly HttpClient Client = new();

        public static async Task Handle(Deliver msg, OnboardingCallbackOptions options, ILogger<Deliver> logger)
        {
            var body = new
            {
                correlationId = msg.CorrelationId,
                merchantId = msg.MerchantId,
                merchantKey = msg.MerchantKey,
                status = msg.Status
            };
            var rawBody = JsonConvert.SerializeObject(body);
            var signature = ComputeSignature(options.CallbackSecret, rawBody);

            using var content = new StringContent(rawBody, Encoding.UTF8, "application/json");
            content.Headers.Add(SignatureHeader, signature);

            var response = await Client.PostAsync(msg.CallbackUrl, content);
            // 2xx dışı → exception → Wolverine durable retry. Credential PG'de kalıcı, kaybolmaz.
            response.EnsureSuccessStatusCode();
            // MerchantKey loglanmaz — yalnız correlation + merchantId (opak tutamaç) + statü.
            logger.LogInformation("Credential callback teslim edildi: {CorrelationId} / {MerchantId} → {Status}",
                msg.CorrelationId, msg.MerchantId, msg.Status);
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