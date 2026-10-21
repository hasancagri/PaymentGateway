namespace Payment.Api.Domains.PaymentLinks.ValueObjects;

/// <summary>
/// 090 (FR-004, İlke VII anti-corruption): bir banka çekiminin NORMALİZE sonucu. Her adapter kendi ham
/// wire kodunu (pos-master `proc_return_code`/`error_code` aynası) bu ortak şekle çevirir — onay VEYA
/// red + 4-sınıf taksonomi. Red `Ok(outcome)` ile taşınır, ResultDomain.Error'a eşlenmez (İlke IV). Kart
/// verisi/sır İÇERMEZ (İlke VI). VO: private ctor + static factory.
/// </summary>
public sealed record BankChargeOutcome
{
    public bool IsApproved { get; }

    /// <summary>Red sınıfı (failover kararı); onayda null.</summary>
    public DeclineClass? DeclineClass { get; }

    /// <summary>Opak banka işlem referansı (gelecekte durum-sorgu/mutabakat); sır değil.</summary>
    public string? ProviderRef { get; }

    /// <summary>Onay kodu (onayda).</summary>
    public string? AuthCode { get; }

    /// <summary>Banka ham kodu (denetim).</summary>
    public string RawCode { get; }

    /// <summary>Çekimde bankaya gönderilen OrderId (orgOrderId) — sonraki Void/Refund/StatusHistory referansı.
    /// Adapter `with` ile set eder (çağrı anında bilinir). Sır değil.</summary>
    public string? BankOrderId { get; init; }

    private BankChargeOutcome(bool isApproved, DeclineClass? declineClass, string? providerRef, string? authCode, string rawCode)
    {
        IsApproved = isApproved;
        DeclineClass = declineClass;
        ProviderRef = providerRef;
        AuthCode = authCode;
        RawCode = rawCode;
    }

    /// <summary>Onaylı çekim.</summary>
    public static BankChargeOutcome Approved(string providerRef, string authCode, string rawCode)
        => new(true, null, providerRef, authCode, rawCode);

    /// <summary>Reddedilen çekim + red sınıfı (failover taksonomisi).</summary>
    public static BankChargeOutcome Declined(DeclineClass declineClass, string rawCode, string? providerRef = null)
        => new(false, declineClass, providerRef, null, rawCode);
}
