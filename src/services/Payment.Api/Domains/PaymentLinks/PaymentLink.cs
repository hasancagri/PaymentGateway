using Common.Domains;
using Payment.Api.Constants;

namespace Payment.Api.Domains.PaymentLinks;

/// <summary>
/// 089: Bir sipariş için tek-kullanımlık, TTL'li pay-by-link ödeme oturumu/ömrü. Kart verisi TAŞIMAZ
/// (anayasa İlke VI — PAN PG'ye girmez). Yaşam döngüsü: Pending → Opened → terminal {Paid, Expired,
/// Cancelled}. Paid bu dilimde erişilmez (sağlayıcı/çekim sonraki dilim — stub). Terminal tek-yön (FR-008).
/// OpenToken = kimliksiz müşteri açılış ucunun beyan-edilen yetkisi (gizli, fail-closed, İlke V).
/// Süre dolumu LAZY: erişim anında (Open/Expire/IsPayable) hesaplanır — proaktif watchdog YOK.
/// </summary>
public class PaymentLink : AggregateRoot
{
    private const string TryCurrency = "TRY";

    // Marten Newtonsoft private-setter deserializasyonu için parametresiz ctor (NonPublicSetters).
    private PaymentLink() { }

    public Guid MerchantId { get; private set; }
    public string OrderRef { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string ReturnUrl { get; private set; } = string.Empty;
    public string StoreName { get; private set; } = string.Empty;
    public string OrderSummary { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public string OpenToken { get; private set; } = string.Empty;
    public PaymentLinkStatus Status { get; private set; }

    // 090: ödeme modeli (bu versiyonda hep NonSecure; 3DS değeri rezerve). Create'te set edilir.
    public PaymentModel PaymentModel { get; private set; }

    // 090: çekim denemeleri (İlke II — koleksiyon private, okuma IReadOnlyList, mutasyon yalnız RecordAttempt).
    private readonly List<PaymentAttempt> _attempts = new();
    public IReadOnlyList<PaymentAttempt> Attempts => _attempts;

    /// <summary>Terminal (Paid/Expired/Cancelled) mı — saf getter.</summary>
    public bool IsTerminal => Status is PaymentLinkStatus.Paid or PaymentLinkStatus.Expired or PaymentLinkStatus.Cancelled;

    /// <summary>Yeni ödeme linki üretir (Pending). Yalnız TRY (FR-004), tutar &gt; 0; orderRef/returnUrl/
    /// storeName/orderSummary/openToken zorunlu; expiresAt gelecekte olmalı. OpenToken + expiresAt handler'dan
    /// (tahmin-edilemez sır + CreatedTime + Ttl).</summary>
    public static ResultDomain<PaymentLink> Create(
        Guid merchantId, string orderRef, decimal amount, string currency, string returnUrl,
        string storeName, string orderSummary, string openToken, DateTime expiresAt)
    {
        if (!string.Equals(currency, TryCurrency, StringComparison.OrdinalIgnoreCase))
            return ResultDomain<PaymentLink>.Error(new MessageItem { Code = PaymentLinkResourceConstants.UNSUPPORTED_CURRENCY });

        if (amount <= 0 || string.IsNullOrWhiteSpace(orderRef) || string.IsNullOrWhiteSpace(returnUrl) ||
            string.IsNullOrWhiteSpace(storeName) || string.IsNullOrWhiteSpace(orderSummary) ||
            string.IsNullOrWhiteSpace(openToken))
            return ResultDomain<PaymentLink>.Error(new MessageItem { Code = PaymentLinkResourceConstants.INVALID_REQUEST });

        var link = new PaymentLink
        {
            MerchantId = merchantId,
            OrderRef = orderRef,
            Amount = amount,
            Currency = TryCurrency,
            ReturnUrl = returnUrl,
            StoreName = storeName,
            OrderSummary = orderSummary,
            OpenToken = openToken,
            Status = PaymentLinkStatus.Pending,
            PaymentModel = PaymentModel.NonSecure
        };

        if (expiresAt <= link.CreatedTime)
            return ResultDomain<PaymentLink>.Error(new MessageItem { Code = PaymentLinkResourceConstants.INVALID_REQUEST });

        link.ExpiresAt = expiresAt;
        return ResultDomain<PaymentLink>.Ok(link);
    }

    /// <summary>Müşteri linki açar (Pending→Opened). Terminal → ilgili red. now &gt; ExpiresAt → lazy Expired +
    /// LINK_EXPIRED. Zaten Opened → idempotent no-op (FR-005). Süre dolumu erişim anında hesaplanır (FR-009).</summary>
    public ResultDomain Open(DateTime now)
    {
        if (Status == PaymentLinkStatus.Cancelled)
            return ResultDomain.Error(new MessageItem { Code = PaymentLinkResourceConstants.LINK_CANCELLED });
        if (Status == PaymentLinkStatus.Expired)
            return ResultDomain.Error(new MessageItem { Code = PaymentLinkResourceConstants.LINK_EXPIRED });
        if (Status == PaymentLinkStatus.Paid)
            return ResultDomain.Error(new MessageItem { Code = PaymentLinkResourceConstants.TERMINAL_VIOLATION });

        if (now > ExpiresAt)
        {
            Status = PaymentLinkStatus.Expired;
            UpdatedTime = now;
            return ResultDomain.Error(new MessageItem { Code = PaymentLinkResourceConstants.LINK_EXPIRED });
        }

        if (Status == PaymentLinkStatus.Opened) return ResultDomain.Ok();

        Status = PaymentLinkStatus.Opened;
        UpdatedTime = now;
        return ResultDomain.Ok();
    }

    /// <summary>Lazy süre dolumu: aktif (Pending/Opened) link now &gt; ExpiresAt ise Expired'a geçer. Terminal
    /// ya da henüz süresi geçmemiş link için no-op Ok (FR-009; proaktif watchdog YOK).</summary>
    public ResultDomain Expire(DateTime now)
    {
        if (IsTerminal) return ResultDomain.Ok();
        if (now <= ExpiresAt) return ResultDomain.Ok();

        Status = PaymentLinkStatus.Expired;
        UpdatedTime = now;
        return ResultDomain.Ok();
    }

    /// <summary>Ödeme (çekim) başlatılabilir mi — saf guard (durum DEĞİŞTİRMEZ, lazy Expire hariç). Opened/
    /// Pending + now &lt;= ExpiresAt + grace → Ok. Grace penceresi aşıldı → lazy Expired + LINK_EXPIRED. Terminal
    /// → ilgili red. Çekim sağlayıcı entegrasyonu sonraki dilim; bu dilimde çağıran stub döner (FR-010).</summary>
    public ResultDomain IsPayable(DateTime now, TimeSpan grace) => PayableGuard(now, grace);

    /// <summary>090 (FR-001/FR-002/FR-003): Bir banka çekim denemesini kaydeder. Önce ödenebilirlik guard'ı
    /// (terminal/iptal/süre+grace — Paid link ikinci çekim YAPAMAZ = çift-çekim guard, FR-002). Guard geçerse
    /// deneme (hangi banka + sonuç + red sınıfı + banka referansı + onay kodu + zaman) link altına eklenir.
    /// Onay → Status=Paid (terminal, tek-yön). Red → Opened kalır (yeni deneme mümkün); red outcome'da taşınır,
    /// ResultDomain.Error değil (İlke IV outcome-enum). Kart verisi İÇERMEZ (İlke VI).</summary>
    public ResultDomain RecordAttempt(string bankCode, BankChargeOutcome outcome, DateTime now, TimeSpan grace)
    {
        var guard = PayableGuard(now, grace);
        if (!guard.IsSuccess) return guard;

        _attempts.Add(PaymentAttempt.From(bankCode, outcome, now));

        if (outcome.IsApproved)
            Status = PaymentLinkStatus.Paid;

        UpdatedTime = now;
        return ResultDomain.Ok();
    }

    /// <summary>Çekim-sonrası ters işlem (Void/Refund) denetim kaydı — ödenebilirlik guard'ı YOK (Paid link
    /// üzerinde çalışır). Durumu değiştirmez (para durumu bankada; store webhook ile bilgilenir). Yalnız denetim.</summary>
    public void RecordReversal(string bankCode, BankChargeOutcome outcome, DateTime now)
    {
        _attempts.Add(PaymentAttempt.From(bankCode, outcome, now));
        UpdatedTime = now;
    }

    // Ödenebilirlik invariant'ı (guard helper — IsPayable + RecordAttempt paylaşır; 3. kopya tutarsızlık
    // üretmesin diye çıkarıldı, conventions guard istisnası). Lazy Expired flip içerir (durum değiştirebilir).
    private ResultDomain PayableGuard(DateTime now, TimeSpan grace)
    {
        if (Status == PaymentLinkStatus.Cancelled)
            return ResultDomain.Error(new MessageItem { Code = PaymentLinkResourceConstants.LINK_CANCELLED });
        if (Status == PaymentLinkStatus.Expired)
            return ResultDomain.Error(new MessageItem { Code = PaymentLinkResourceConstants.LINK_EXPIRED });
        if (Status == PaymentLinkStatus.Paid)
            return ResultDomain.Error(new MessageItem { Code = PaymentLinkResourceConstants.TERMINAL_VIOLATION });

        if (now > ExpiresAt + grace)
        {
            Status = PaymentLinkStatus.Expired;
            UpdatedTime = now;
            return ResultDomain.Error(new MessageItem { Code = PaymentLinkResourceConstants.LINK_EXPIRED });
        }

        return ResultDomain.Ok();
    }

    /// <summary>Store linki iptal eder (Pending/Opened → Cancelled). Terminal → TERMINAL_VIOLATION (tek-yön,
    /// FR-008). İptal sonrası açma/ödeme reddedilir.</summary>
    public ResultDomain Cancel()
    {
        if (IsTerminal)
            return ResultDomain.Error(new MessageItem { Code = PaymentLinkResourceConstants.TERMINAL_VIOLATION });

        Status = PaymentLinkStatus.Cancelled;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }
}

// Enum aggregate dosyasında (conventions — ayrı dosya/Enumeration base yok). Aktif = {Pending, Opened}.
public enum PaymentLinkStatus
{
    Pending = 0,
    Opened = 1,
    Paid = 2,
    Expired = 3,
    Cancelled = 4
}

// 090: ödeme modeli. Bu versiyonda yalnız NonSecure (3DS'siz sale); 3DS değeri sonraki versiyonda eklenecek.
public enum PaymentModel
{
    NonSecure = 0
}

// 090 (FR-004): normalize red taksonomisi — failover kararını yönetir. Issuer/Fraud → failover YOK
// (aynı issuer boşuna; fraud asla retry); Technical/Config → sıradaki uygun bankaya geçilir (FR-005).
public enum DeclineClass
{
    IssuerDecline = 0,
    TechnicalFailure = 1,
    ConfigError = 2,
    FraudDecline = 3
}
