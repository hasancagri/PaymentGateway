namespace Payment.Api.Domains.PaymentLinks;

/// <summary>
/// 090 (FR-003): PaymentLink altında tek bir banka çekim denemesi — child entity (AggregateRoot DEĞİL,
/// base ALMAZ). Yalnız PaymentLink.RecordAttempt içinde üretilir (İlke II). Red denemeleri denetim/
/// mutabakat için saklanır (silinmez). Kart verisi İÇERMEZ (İlke VI — yalnız banka kodu/ref/onay kodu/ham kod).
/// </summary>
public class PaymentAttempt
{
    // Marten Newtonsoft private-setter deserializasyonu için parametresiz ctor (NonPublicSetters).
    private PaymentAttempt() { }

    public string BankCode { get; private set; } = string.Empty;
    public bool IsApproved { get; private set; }
    public DeclineClass? DeclineClass { get; private set; }
    public string? ProviderRef { get; private set; }
    public string? AuthCode { get; private set; }
    public string RawCode { get; private set; } = string.Empty;
    public string? BankOrderId { get; private set; } // orgOrderId — Void/Refund/Status referansı
    public DateTime AttemptedAt { get; private set; }

    // Normalize outcome'u denetim kaydına düzleştirir. Yalnız RecordAttempt çağırır (İlke II).
    internal static PaymentAttempt From(string bankCode, BankChargeOutcome outcome, DateTime now)
        => new()
        {
            BankCode = bankCode,
            IsApproved = outcome.IsApproved,
            DeclineClass = outcome.DeclineClass,
            ProviderRef = outcome.ProviderRef,
            AuthCode = outcome.AuthCode,
            RawCode = outcome.RawCode,
            BankOrderId = outcome.BankOrderId,
            AttemptedAt = now
        };
}
