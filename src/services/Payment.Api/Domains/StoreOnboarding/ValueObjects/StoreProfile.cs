using System.Numerics;

namespace Payment.Api.Domains.StoreOnboarding.ValueObjects;

/// <summary>
/// 091 (FR-003): Store'un bankalara iletilecek ortak ÇEKİRDEK profili (canonical). Tek sabit hosted formun
/// topladığı set — banka farkı adapter'da, burada değil (research D2 yalın çekirdek). VO: record + private
/// ctor + static Create. Tüm alanlar zorunlu; IBAN TR mod-97, MCC 4 hane. Kart verisi/sır İÇERMEZ (İlke VI).
/// </summary>
public sealed record StoreProfile
{
    public string LegalName { get; private set; }
    public string TaxNumber { get; private set; }
    public string Iban { get; private set; }
    public string Mcc { get; private set; }
    public string WebsiteUrl { get; private set; }
    public string ContactName { get; private set; }
    public string ContactEmail { get; private set; }

    // Marten Newtonsoft private-setter deserializasyonu için parametresiz ctor (NonPublicSetters).
    private StoreProfile() { }

    private StoreProfile(string legalName, string taxNumber, string iban, string mcc,
        string websiteUrl, string contactName, string contactEmail)
    {
        LegalName = legalName;
        TaxNumber = taxNumber;
        Iban = iban;
        Mcc = mcc;
        WebsiteUrl = websiteUrl;
        ContactName = contactName;
        ContactEmail = contactEmail;
    }

    /// <summary>Çekirdek profili doğrular + üretir. Boş alan / geçersiz IBAN (TR mod-97) / 4-hane dışı MCC /
    /// biçimsiz URL-email → INVALID_PROFILE. IBAN normalize (boşluk/büyük harf) edilerek saklanır.</summary>
    public static ResultDomain<StoreProfile> Create(
        string legalName, string taxNumber, string iban, string mcc,
        string websiteUrl, string contactName, string contactEmail)
    {
        if (string.IsNullOrWhiteSpace(legalName) || string.IsNullOrWhiteSpace(taxNumber) ||
            string.IsNullOrWhiteSpace(iban) || string.IsNullOrWhiteSpace(mcc) ||
            string.IsNullOrWhiteSpace(websiteUrl) || string.IsNullOrWhiteSpace(contactName) ||
            string.IsNullOrWhiteSpace(contactEmail))
            return Invalid();

        var normalizedIban = iban.Replace(" ", "").ToUpperInvariant();
        if (!IsValidTrIban(normalizedIban))
            return Invalid();

        if (mcc.Length != 4 || !mcc.All(char.IsDigit))
            return Invalid();

        if (!websiteUrl.Contains('.') || websiteUrl.Contains(' '))
            return Invalid();

        if (!contactEmail.Contains('@') || !contactEmail.Contains('.') || contactEmail.Contains(' '))
            return Invalid();

        return ResultDomain<StoreProfile>.Ok(new StoreProfile(
            legalName.Trim(), taxNumber.Trim(), normalizedIban, mcc, websiteUrl.Trim(),
            contactName.Trim(), contactEmail.Trim()));
    }

    private static ResultDomain<StoreProfile> Invalid()
        => ResultDomain<StoreProfile>.Error(new MessageItem { Code = StoreOnboardingResourceConstants.INVALID_PROFILE });

    // TR IBAN: 26 karakter, ilk 4'ü sona taşı, harf→sayı (A=10..Z=35), mod 97 == 1 (ISO 7064).
    private static bool IsValidTrIban(string iban)
    {
        if (iban.Length != 26 || !iban.StartsWith("TR")) return false;
        var rearranged = iban[4..] + iban[..4];
        var digits = new System.Text.StringBuilder();
        foreach (var ch in rearranged)
        {
            if (char.IsDigit(ch)) digits.Append(ch);
            else if (ch is >= 'A' and <= 'Z') digits.Append((ch - 'A' + 10).ToString());
            else return false;
        }
        return BigInteger.TryParse(digits.ToString(), out var value) && value % 97 == 1;
    }
}
