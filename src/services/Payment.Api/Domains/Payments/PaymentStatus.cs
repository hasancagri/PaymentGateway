namespace Payment.Api.Domains.Payments;

/// <summary>
/// Çekim sonucu (033). Cancel/Refund statüleri ayrı işte gelir (bu kayıt onların temeli).
/// </summary>
public enum PaymentStatus
{
    Success = 1,
    Failed = 2
}
