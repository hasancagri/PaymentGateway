using Common.Domains;
using Payment.Api.Constants;

namespace Payment.Api.Domains.HostedPayments;

/// <summary>
/// 041: Bir hosted-CF ödeme denemesi (iyzico Checkout Form). Kart verisi TAŞIMAZ (yalnız iyzico'da).
/// Yaşam döngüsü: Start → Pending → (MarkSucceeded | MarkFailed) terminal. Terminal tek-yön (başarılı →
/// başarılı kalır, FR-008). CallbackToken = kimliksiz iyzico-callback ucunun beyan-edilen yetkisi (C1).
/// </summary>
public class HostedPaymentSession : AggregateRoot
{
    // Marten Newtonsoft private-setter deserializasyonu için parametresiz ctor (NonPublicSetters).
    private HostedPaymentSession() { }

    public Guid MerchantId { get; private set; }
    public string OrderRef { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string CallbackUrl { get; private set; } = string.Empty;
    public string CallbackToken { get; private set; } = string.Empty;
    public string CheckoutFormToken { get; private set; } = string.Empty;
    public string HostedUrl { get; private set; } = string.Empty;
    public HostedPaymentStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public string? ProviderPaymentId { get; private set; }

    /// <summary>Terminal (Succeeded/Failed) mı — saf getter.</summary>
    public bool IsTerminal => Status is HostedPaymentStatus.Succeeded or HostedPaymentStatus.Failed;

    /// <summary>Yeni hosted ödeme girişimi başlatır (Pending). CallbackToken = tahmin-edilemez per-session
    /// sır (handler üretir). Amount>0, orderRef/callbackUrl/callbackToken zorunlu.</summary>
    public static ResultDomain<HostedPaymentSession> Start(
        Guid merchantId, string orderRef, decimal amount, string callbackUrl, string callbackToken)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(orderRef) ||
            string.IsNullOrWhiteSpace(callbackUrl) || string.IsNullOrWhiteSpace(callbackToken))
            return ResultDomain<HostedPaymentSession>.Error(new MessageItem
            { Code = HostedPaymentResourceConstants.INVALID_REQUEST });

        return ResultDomain<HostedPaymentSession>.Ok(new HostedPaymentSession
        {
            MerchantId = merchantId,
            OrderRef = orderRef,
            Amount = amount,
            CallbackUrl = callbackUrl,
            CallbackToken = callbackToken,
            Status = HostedPaymentStatus.Pending
        });
    }

    /// <summary>CF initialize başarısından sonra iyzico token'ını + hosted sayfa URL'ini bağlar (yalnız
    /// Pending iken). HostedUrl saklanır → idempotent tekrar-başlatmada aynen döner.</summary>
    public ResultDomain AttachCheckoutForm(string checkoutFormToken, string hostedUrl)
    {
        if (IsTerminal || string.IsNullOrWhiteSpace(checkoutFormToken) || string.IsNullOrWhiteSpace(hostedUrl))
            return ResultDomain.Error(new MessageItem { Code = HostedPaymentResourceConstants.TERMINAL_VIOLATION });

        CheckoutFormToken = checkoutFormToken;
        HostedUrl = hostedUrl;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Ödemeyi başarılı terminal duruma taşır. Zaten Succeeded ise idempotent no-op (ilk sonuç
    /// korunur); Failed iken çağrılırsa terminal ihlali RET (FR-008).</summary>
    public ResultDomain MarkSucceeded(string providerPaymentId)
    {
        if (Status == HostedPaymentStatus.Succeeded) return ResultDomain.Ok();
        if (Status == HostedPaymentStatus.Failed)
            return ResultDomain.Error(new MessageItem { Code = HostedPaymentResourceConstants.TERMINAL_VIOLATION });

        Status = HostedPaymentStatus.Succeeded;
        ProviderPaymentId = providerPaymentId;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Ödemeyi başarısız terminal duruma taşır. Zaten Failed ise idempotent no-op (ilk sebep
    /// korunur); Succeeded iken çağrılırsa terminal ihlali RET (FR-008).</summary>
    public ResultDomain MarkFailed(string reasonCode)
    {
        if (Status == HostedPaymentStatus.Failed) return ResultDomain.Ok();
        if (Status == HostedPaymentStatus.Succeeded)
            return ResultDomain.Error(new MessageItem { Code = HostedPaymentResourceConstants.TERMINAL_VIOLATION });

        Status = HostedPaymentStatus.Failed;
        FailureReason = reasonCode;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }
}

// Enum aggregate dosyasında (conventions — ayrı dosya/Enumeration base yok).
public enum HostedPaymentStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2
}
