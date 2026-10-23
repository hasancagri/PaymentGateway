namespace Payment.Api.Options;

// 090 (FR-017, İlke VII): Acquirer başına yönlendirme ÖNGÖRÜ verisi (non-secret, appsettings). Banka
// otoritedir; bu yalnız PG'nin öngörü kopyasıdır (yetenek eleme + on-us + maliyet sıralaması için).
// Router bu listeyi okur; handler/router'da literal yazılmaz (CLAUDE.md).
public class PosAccountOptions
{
    /// <summary>Tanımlı acquirer öngörü kayıtları (banka başına bir giriş).</summary>
    public List<PosAccountProfile> Accounts { get; set; } = new();
}

// Tek acquirer öngörüsü. BankCode adapter.BankCode ile eşleşir; on-us için kart issuer'ıyla kıyaslanır.
public class PosAccountProfile
{
    /// <summary>Acquirer banka kodu — IBankPaymentAdapter.BankCode ve BinCard.IssuerBankCode ile eşleşir.</summary>
    public string BankCode { get; set; } = string.Empty;

    /// <summary>093: Müşteriye gösterilecek banka adı (taksit ekranında). Boşsa BankCode gösterilir.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Desteklenen kart tipleri (credit/debit) — yetenek elemesinde kullanılır.</summary>
    public string[] SupportedCardTypes { get; set; } = Array.Empty<string>();

    /// <summary>NonSecure (3DS'siz) sale destekler mi — bu versiyonda hep true (3DS seam).</summary>
    public bool SupportsNonSecure { get; set; } = true;

    // 092: statik CostPriority KALDIRILDI; routing maliyeti artık BankCommissionRate read-model'inden hesaplanır
    // (banka-beslemeli öngörü). PosAccount yalnız YETENEK (kart tipi/işlem) öngörüsü tutar — oran burada DEĞİL.
}
