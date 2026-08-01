namespace Commission.Api.Domains.SharedKernel;

/// <summary>Commission BC'ye özel doğrulama mesaj kodları (Result pattern; serbest metin değil).</summary>
public static class CommissionResourceConstants
{
    public const string MERCHANT_RATE_MUST_EXCEED_BANK_RATE = "MERCHANT_RATE_MUST_EXCEED_BANK_RATE";

    /// <summary>Bağlı komisyon kaydı olan banka silinmeye çalışıldığında.</summary>
    public const string BANK_HAS_COMMISSIONS = "BANK_HAS_COMMISSIONS";

    /// <summary>Kanonik katalogda bulunmayan bir kodla banka eklenmeye çalışıldığında.</summary>
    public const string BANK_NOT_IN_CATALOG = "BANK_NOT_IN_CATALOG";
}