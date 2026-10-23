using Newtonsoft.Json;

namespace Payment.Api.Domains.StoreOnboarding.BankOnboarding;

/// <summary>
/// 091 (İlke VII): simüle banka onboarding adapter'ı (stub). Gerçek banka intake'i + çalışan-onay yüzeyi
/// ayrı BankSimulator repo'sunda (091 DIŞI) — bu adapter port'u bir kez doldurur, gerçek banka gelince
/// değişir (anti-corruption sınırı sabit kalır). Wire DTO'ları private nested (domain sınırını geçmez).
/// SubmitReferral çekirdeği doğrular → Submitted ack (banka-özel ekstra alan konsepti simülatörde yok → B-red
/// yolu burada tetiklenmez, port yeteneği olarak durur). Asıl onay sonra webhook'la gelir (NormalizeCallback).
/// </summary>
public sealed class SimulatorBankOnboardingAdapter : IBankOnboardingAdapter
{
    public string BankCode { get; }

    public SimulatorBankOnboardingAdapter(string bankCode) => BankCode = bankCode;

    public Task<ResultDomain<OnboardingOutcome>> SubmitReferralAsync(OnboardingReferral request, CancellationToken ct)
    {
        // Çekirdek profil StoreProfile.Create ile zaten doğrulandı → simülatör başvuruyu kuyruklar (Submitted).
        // Gerçek bankada banka-özel zorunlu alan eksikse burada Rejected(BANK_FIELD_MISSING) dönerdi (B kararı).
        var bankApplicationRef = $"{BankCode}-{Guid.NewGuid():N}";
        return Task.FromResult(ResultDomain<OnboardingOutcome>.Ok(OnboardingOutcome.Submitted(bankApplicationRef)));
    }

    public Task<ResultDomain<OnboardingOutcome>> QueryApplicationStatusAsync(string bankApplicationRef, CancellationToken ct)
        // Simülatörde çalışan-onay yok → hâlâ "inceleniyor" (Submitted). Reconcile erken terminal vermez (FR-012).
        => Task.FromResult(ResultDomain<OnboardingOutcome>.Ok(OnboardingOutcome.Submitted(bankApplicationRef)));

    public ResultDomain<OnboardingOutcome> NormalizeCallback(string rawBody)
    {
        // Banka webhook ham gövdesi → normalize OnboardingOutcome (anti-corruption). Her bankanın tel formatı
        // farklı olabilir; simülatör ortak JSON şekli kullanır (contracts/onboarding-webhooks.md).
        var wire = JsonConvert.DeserializeObject<CallbackWire>(rawBody);
        if (wire is null)
            return ResultDomain<OnboardingOutcome>.Error(new MessageItem { Code = StoreOnboardingResourceConstants.CALLBACK_REJECTED });

        var outcome = wire.Outcome switch
        {
            "Approved" when wire.Credential is not null => OnboardingOutcome.Approved(
                BankMerchantCredential.Create(
                    wire.Credential.ShopCode, wire.Credential.UserCode, wire.Credential.UserPass,
                    wire.Credential.SecretKey, wire.Credential.SubscriptionKey),
                wire.BankApplicationRef ?? string.Empty),
            "Rejected" => OnboardingOutcome.Rejected(wire.ReasonCode ?? StoreOnboardingResourceConstants.CALLBACK_REJECTED),
            _ => null
        };

        return outcome is null
            ? ResultDomain<OnboardingOutcome>.Error(new MessageItem { Code = StoreOnboardingResourceConstants.CALLBACK_REJECTED })
            : ResultDomain<OnboardingOutcome>.Ok(outcome);
    }

    // Banka inbound webhook wire tipi — adapter İÇİNDE private nested (domain sınırını geçmez, İlke VII).
    private sealed class CallbackWire
    {
        public string BankCode { get; set; } = string.Empty;
        public string Outcome { get; set; } = string.Empty;
        public string? ReasonCode { get; set; }
        public string? BankApplicationRef { get; set; }
        public CredentialWire? Credential { get; set; }
    }

    private sealed class CredentialWire
    {
        public string ShopCode { get; set; } = string.Empty;
        public string UserCode { get; set; } = string.Empty;
        public string UserPass { get; set; } = string.Empty;
        public string? SecretKey { get; set; }
        public string? SubscriptionKey { get; set; }
    }
}
