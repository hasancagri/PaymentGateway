using System.Security.Cryptography;
using System.Text;

namespace Payment.Api.Domains.StoreOnboarding.Features.Commands;

/// <summary>
/// 091 US2 (FR-013, İlke VI): Store'a giden "Live oldun" HMAC-imzalı dayanıklı bildirim (StoreResultDelivery
/// aynası, onboarding yönü). ReceiveBankOnboardingOutcome handler [Transactional] içinde bus.PublishAsync(Deliver)
/// → yalnız DB commit'te gider (outbox, kayıpsız). Handler ham JSON gövde üretir, X-Signature =
/// HMAC-SHA256(CallbackSecret, raw_body) lowercase hex ile CallbackUrl'e POST eder; non-2xx → throw →
/// Wolverine durable retry. Gövde CREDENTIAL İÇERMEZ (yalnız "Live" + aktive eden banka). CallbackSecret
/// secret-store'dan; ASLA log/trace/body/dönüşte. Sınıf adı TEKİL "Handler" (çoğul keşfedilmez).
/// </summary>
public static class OnboardingResultDelivery
{
    public const string SignatureHeader = "X-Signature";

    public record Deliver(
        string CallbackUrl, string PgStoreRef, string MerchantId, string Status,
        string ActivatedBankCode, DateTime ActivatedAt);

    public static class DeliverHandler
    {
        private static readonly HttpClient Client = new();

        public static async Task Handle(Deliver msg, OnboardingStoreCallbackOptions options, ILogger<Deliver> logger)
        {
            var body = new
            {
                pgStoreRef = msg.PgStoreRef,
                merchantId = msg.MerchantId,
                status = msg.Status,
                activatedBankCode = msg.ActivatedBankCode,
                activatedAt = msg.ActivatedAt
            };
            var rawBody = JsonConvert.SerializeObject(body);
            var signature = ComputeSignature(options.CallbackSecret, rawBody);

            using var content = new StringContent(rawBody, Encoding.UTF8, "application/json");
            content.Headers.Add(SignatureHeader, signature);

            var response = await Client.PostAsync(msg.CallbackUrl, content);
            response.EnsureSuccessStatusCode(); // 2xx dışı → exception → durable retry (kayıpsız)
            logger.LogInformation("Onboarding sonucu teslim edildi: {PgStoreRef} → {Status}", msg.PgStoreRef, msg.Status);
        }

        private static string ComputeSignature(string secret, string rawBody)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
        }
    }
}
