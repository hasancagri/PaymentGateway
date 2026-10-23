# Contract: IBankOnboardingAdapter (İlke VII port)

Charge portundan (`IBankPaymentAdapter`) AYRI. Her banka kendi adapter'ıyla gerçekler; wire istek/cevap
tipleri adapter İÇİNDE private nested (domain sınırını geçmez). Paylaşılan SDK YOK. Adapter STATELESS
(durum aggregate'te). İki yön anti-corruption: canonical→wire (referral), wire→canonical (`NormalizeCallback`).

```csharp
public interface IBankOnboardingAdapter
{
    /// <summary>Bu adapter'ın bankası (StoreOnboardingOptions.BankCodes + InterVposBankProfile.BankCode ile eşleşir).</summary>
    string BankCode { get; }

    /// <summary>Canonical referral'ı bankanın intake wire'ına çevirip gönderir. Banka-özel zorunlu alan
    /// çekirdekte YOKSA wire'a gitmeden Rejected(BANK_FIELD_MISSING, reason) döner (B kararı). Ağ/teknik
    /// hata → Rejected(technical) (reconcile sonra toparlar). Başarılı kuyruklama → Submitted(bankApplicationRef).</summary>
    Task<ResultDomain<OnboardingOutcome>> SubmitReferralAsync(OnboardingReferral request, CancellationToken ct);

    /// <summary>Reconcile: bankadaki başvurunun güncel durumunu sorar → Approved(cred,ref) / Rejected(reason) /
    /// hâlâ sürüyor (Submitted). Watchdog kaçan webhook'u buradan toparlar.</summary>
    Task<ResultDomain<OnboardingOutcome>> QueryApplicationStatusAsync(string bankApplicationRef, CancellationToken ct);

    /// <summary>Banka inbound webhook ham gövdesini normalize OnboardingOutcome'a çevirir (anti-corruption).
    /// Approved'da credential'ı çıkarır (yalnız bu yolda). Her bankanın gövdesi farklı → adapter çözer.</summary>
    ResultDomain<OnboardingOutcome> NormalizeCallback(string rawBody);
}

// Canonical referral (domain-görünür). Banka wire'ı adapter içinde türetilir.
public sealed record OnboardingReferral(StoreProfile Profile, string BankCode, string CallbackUrl, string CallbackToken);

// Normalize sonuç VO (record + private ctor + static factory).
// Kind ∈ { Submitted, Approved, Rejected }. Credential YALNIZ Approved + yalnız NormalizeCallback yolunda dolu.
// Factory: Submitted(string bankApplicationRef) · Approved(BankMerchantCredential cred, string bankApplicationRef) · Rejected(string reasonCode)
```

**Bu versiyon:** `SimulatorBankOnboardingAdapter` — alan doğrular, `Submitted` ack döner (gerçek banka
yok); approval webhook'u BankSimulator ekleyince gelir. `QueryApplicationStatusAsync` stub `Submitted`
döner (reconcile no-op). Program.cs'te `StoreOnboardingOptions.BankCodes` / `InterVposBankProfile` üzerinden
banka başına kaydedilir (charge adapter fan-out deseninin aynası).
