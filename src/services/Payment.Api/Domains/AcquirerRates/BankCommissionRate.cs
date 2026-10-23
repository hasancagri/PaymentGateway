namespace Payment.Api.Domains.AcquirerRates;

/// <summary>
/// 093 (İlke VII): Acquirer komisyon oranı READ-MODEL'i — bankanın PG'ye uygulayacağı MDR'ın PG'deki salt-okur
/// ÖNGÖRÜ kopyası (banka OTORİTE). Aggregate DEĞİL; düz Marten dokümanı (kimlik = BankCode). Oran KART AİLESİNE
/// bağlı: (Scheme × CardType × Installment) → oran (093 kullanıcı kararı). Router efektif maliyeti (oran × tutar)
/// bundan hesaplar — per-taksit en avantajlı banka. BAŞLANGIÇTA embedded seed; GERÇEK besleme (banka onboarding
/// onayında MDR handoff + mutabakat düzeltmesi) sonraki dilim. BinCard salt-okur referans deseninin aynası.
/// </summary>
public class BankCommissionRate
{
    // Marten kimliği (Program.cs'te Identity(x => x.BankCode)). Acquirer adapter.BankCode ile eşleşir.
    public string BankCode { get; set; } = string.Empty;

    /// <summary>Kart-ailesi × taksit oran satırları.</summary>
    public List<CommissionRateRow> Rates { get; set; } = new();

    /// <summary>Eşleşen satır yoksa güvenli varsayılan oran (banka adaylıktan DÜŞMEZ).</summary>
    public decimal DefaultCommissionRate { get; set; }

    /// <summary>Kart ailesi (şema+tip) + taksit için öngörü oran — tam eşleşme yoksa DefaultCommissionRate.
    /// Şema/tip null ise (BIN bilinmiyor) yalnız varsayılan (saf getter, domain-TDD).</summary>
    public decimal RateFor(string? scheme, string? cardType, int installment)
    {
        if (scheme is null || cardType is null) return DefaultCommissionRate;
        var row = Rates.FirstOrDefault(r =>
            string.Equals(r.Scheme, scheme, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.CardType, cardType, StringComparison.OrdinalIgnoreCase) &&
            r.Installment == installment);
        return row?.Rate ?? DefaultCommissionRate;
    }
}

/// <summary>Tek oran satırı: kart ailesi (şema+tip) + taksit → oran. Düz doküman alt-nesnesi.</summary>
public class CommissionRateRow
{
    public string Scheme { get; set; } = string.Empty;   // Visa/Mastercard/Troy/Amex
    public string CardType { get; set; } = string.Empty; // credit/debit
    public int Installment { get; set; }                 // 1,2,3,6,9
    public decimal Rate { get; set; }
}
