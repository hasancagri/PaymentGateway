namespace Commission.Api.Domains.SharedKernel;

/// <summary>Commission BC'ye özel doğrulama mesaj kodları (Result pattern; serbest metin değil).</summary>
public static class CommissionResourceConstants
{
    /// <summary>Bağlı komisyon kaydı olan banka silinmeye çalışıldığında.</summary>
    public const string BANK_HAS_COMMISSIONS = "BANK_HAS_COMMISSIONS";

    /// <summary>Kanonik katalogda bulunmayan bir kodla banka eklenmeye çalışıldığında.</summary>
    public const string BANK_NOT_IN_CATALOG = "BANK_NOT_IN_CATALOG";

    // --- 019: komisyon teklifi / taslak pazarlığı ---

    /// <summary>Revizyon sonucu oran ilgili banka oranının (taban) altına indiğinde — işlem BÜTÜN reddedilir.</summary>
    public const string RATE_BELOW_BANK_FLOOR = "RATE_BELOW_BANK_FLOOR";

    /// <summary>Kabul edilmiş (kilitli) taslak üzerinde revizyon denemesi.</summary>
    public const string DRAFT_LOCKED = "DRAFT_LOCKED";

    /// <summary>Merchant'ın kabul edilmiş teklifi varken yeni teklif/revizyon denemesi (FR-012 değişmezlik).</summary>
    public const string PROPOSAL_ALREADY_ACCEPTED = "PROPOSAL_ALREADY_ACCEPTED";

    /// <summary>Karar bileti geçersiz: kullanılmış, süresi dolmuş veya teklif Superseded.</summary>
    public const string PROPOSAL_TICKET_INVALID = "PROPOSAL_TICKET_INVALID";
}