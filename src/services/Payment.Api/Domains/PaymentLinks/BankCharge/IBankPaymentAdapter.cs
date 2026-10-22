namespace Payment.Api.Domains.PaymentLinks.BankCharge;

/// <summary>
/// 090 (İlke VII port/adapter): ortak banka çekim portu. Her banka kendi adapter'ıyla gerçekler; wire
/// tipleri adapter'ın İÇİNDE private nested kalır (domain sınırını geçmez), adapter wire'ı normalize
/// BankChargeOutcome'a çevirir (anti-corruption). Bu versiyonda yalnız NonSecure sale; iptal/iade/durum-
/// sorgu/BIN ileride port'u genişletir (seam, bu versiyonda YOK). Dönüş Ok(outcome) = onay VEYA red
/// (outcome taşır); Error yalnız yönlendirilemez programlama hatası.
/// </summary>
public interface IBankPaymentAdapter
{
    /// <summary>Bu adapter'ın işlediği banka kodu (PosAccount.BankCode + BinCard.IssuerBankCode ile eşleşir).</summary>
    string BankCode { get; }

    /// <summary>NonSecure sale çekimi. request.Card transient (PAN/CVV) — adapter wire'a çevirir, saklamaz/loglamaz (İlke VI).</summary>
    Task<ResultDomain<BankChargeOutcome>> ChargeAsync(BankChargeRequest request, CancellationToken ct);

    /// <summary>Durum sorgu (in-doubt çözümü): bankaya orgOrderId'yi sorar → Approved (çekilmiş, kurtar) /
    /// Declined (çekilmemiş ya da hâlâ belirsiz). Dayanıklı reconcile Process kullanır (çift-çekim emniyeti).</summary>
    Task<ResultDomain<BankChargeOutcome>> QueryStatusAsync(string orderId, CancellationToken ct);

    /// <summary>İptal (açık batch / gün sonu öncesi) — orgOrderId. Desteklemeyen banka Declined(ConfigError) döner.</summary>
    Task<ResultDomain<BankChargeOutcome>> VoidAsync(string bankOrderId, CancellationToken ct);

    /// <summary>İade (kapalı batch / gün sonu sonrası, kısmi) — orgOrderId + tutar.</summary>
    Task<ResultDomain<BankChargeOutcome>> RefundAsync(string bankOrderId, decimal amount, CancellationToken ct);
}

// 090 (İlke VI): kart verisi — SALT TRANSIENT. Yalnız çekim anında yaşar (endpoint body → command →
// request → wire). ASLA saklanmaz/loglanmaz/aggregate'e veya dayanıklı outbox'a girmez.
public sealed record CardData(string Pan, string HolderName, string ExpiryMonth, string ExpiryYear, string Cvv);

// 090: tek çekim isteği (transient — kart dahil). Saklanmaz (İlke VI).
public sealed record BankChargeRequest(CardData Card, decimal Amount, string Currency, string OrderRef, Guid PaymentLinkId);
