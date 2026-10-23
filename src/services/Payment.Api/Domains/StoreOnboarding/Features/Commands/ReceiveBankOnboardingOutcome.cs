using System.Security.Cryptography;
using System.Text;
using Payment.Api.Domains.StoreOnboarding;
using Payment.Api.Domains.StoreOnboarding.BankOnboarding;
using Payment.Api.Domains.StoreOnboarding.Credentials;

namespace Payment.Api.Domains.StoreOnboarding.Features.Commands;

/// <summary>
/// 091 US2 (FR-007/FR-008/FR-009): Banka→PG onboarding sonucu webhook'u. CallbackToken path kapısı + X-Signature
/// HMAC-SHA256 (banka InboundCallbackSecret ile, ham gövde) doğrulanır; eşleşmezse CALLBACK_REJECTED (durum
/// değişmez, fail-closed). Geçerliyse adapter NormalizeCallback → OnboardingOutcome; Approved'da credential
/// at-rest şifreli saklanır (IBankCredentialStore → opak CredentialRef) ve RecordBankOutcome çağrılır. İLK
/// onayda store Live → OnboardingResultDelivery (store'a imzalı "Live" webhook, credential'sız). Idempotent
/// (tekrar gelen onay no-op). Credential ASLA log/trace/dönüşte (İlke VI). Sınıf adı TEKİL "Handler".
/// </summary>
public static class ReceiveBankOnboardingOutcome
{
    public const string SignatureHeader = "X-Signature";

    public record Receive(string CallbackToken, string BankCode, string RawBody, string Signature);

    [Transactional]
    public class Handler
    {
        public async Task<FeatureResultModel> Handle(
            Receive msg, IDocumentSession session, IEnumerable<IBankOnboardingAdapter> adapters,
            InterVposBankOptions bankOptions, IBankCredentialStore credentialStore, IMessageBus bus,
            OnboardingStoreCallbackOptions storeCallback, CancellationToken ct)
        {
            var onboarding = await session.Query<StoreOnboarding>()
                .Where(x => x.CallbackToken == msg.CallbackToken)
                .FirstOrDefaultAsync(ct);
            if (onboarding is null)
                return Rejected();

            // İmza doğrula: bankanın InboundCallbackSecret'ıyla ham gövde HMAC (fail-closed; secret boşsa reddet).
            var profile = bankOptions.Banks.FirstOrDefault(b => b.BankCode == msg.BankCode);
            if (profile is null || string.IsNullOrEmpty(profile.InboundCallbackSecret) ||
                !VerifySignature(profile.InboundCallbackSecret, msg.RawBody, msg.Signature))
                return Rejected();

            var adapter = adapters.FirstOrDefault(a => a.BankCode == msg.BankCode);
            if (adapter is null)
                return Rejected();

            var normalized = adapter.NormalizeCallback(msg.RawBody);
            if (!normalized.IsSuccess || normalized.Data is null)
                return FeatureResultModel.Error(normalized.Messages);
            var outcome = normalized.Data;

            // Approved → credential'ı at-rest şifreli sakla (opak CredentialRef); sır aggregate'e/dönüşe girmez (İlke VI).
            Guid? credentialRef = null;
            if (outcome.Kind == OnboardingOutcomeKind.Approved && outcome.Credential is not null)
                credentialRef = await credentialStore.SaveAsync(onboarding.MerchantId, msg.BankCode, outcome.Credential, ct);

            var now = DateTime.UtcNow;
            var wasLive = onboarding.IsLive;
            var recorded = onboarding.RecordBankOutcome(msg.BankCode, outcome, credentialRef, now);
            if (!recorded.IsSuccess)
                return FeatureResultModel.Error(recorded.Messages);

            session.Store(onboarding);

            // İlk aktivasyon → store'a "Live" bildir (dayanıklı HMAC webhook; credential İÇERMEZ, FR-013).
            if (onboarding.IsLive && !wasLive)
                await bus.PublishAsync(new OnboardingResultDelivery.Deliver(
                    storeCallback.CallbackUrl, onboarding.Id.ToString(), onboarding.MerchantId.ToString(),
                    nameof(OnboardingStatus.Live), msg.BankCode, now));

            return FeatureResultModel.Ok();
        }

        private static FeatureResultModel Rejected()
            => FeatureResultModel.Error(new MessageItem { Code = StoreOnboardingResourceConstants.CALLBACK_REJECTED });

        // HMAC-SHA256(secret, raw_body) → lowercase hex; sabit-zamanlı karşılaştırma (timing guard). Sır loglanmaz.
        private static bool VerifySignature(string secret, string rawBody, string signature)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature ?? string.Empty));
        }
    }
}
