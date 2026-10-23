namespace Payment.Api.Options;

// 091 DEV-ONLY: onboarding onay/red trafiğini atlayıp "baştan Live" bir store seed'ler (test hızlandırma).
// YALNIZ Development'ta uygulanır (Program.cs IsDevelopment kapısı) — prod'da ASLA çalışmaz. Sır içermez
// (yalnız Live durum seed'i; banka credential'ı seed'lenmez — charge D9 wiring ayrı). Options validation YOK
// (prod'da section bulunmak zorunda değil); composition root'ta doğrudan okunur (dev seed istisnası).
public class StoreOnboardingDevSeedOptions
{
    /// <summary>Dev seed açık mı (yalnız Development'ta etkili).</summary>
    public bool Enabled { get; set; }

    /// <summary>Baştan Live yapılacak store kimliği (ödeme testinde aynı MerchantId kullanılır).</summary>
    public string MerchantId { get; set; } = string.Empty;

    /// <summary>Seed store adı.</summary>
    public string StoreName { get; set; } = "Dev Seed Store";

    /// <summary>Onaylı (Approved) kabul edilecek banka kodu (BankCodes içinden biri).</summary>
    public string BankCode { get; set; } = string.Empty;
}
