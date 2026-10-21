namespace Payment.Api.Domains.PaymentLinks.BankCharge;

/// <summary>
/// 3D Secure yeteneği — her adapter desteklemez (InterVpos eder; TuruncuBank bu versiyonda etmez). 3DS'te
/// kart tarayıcıdan bankaya gider (PG sunucusuna uğramaz, İlke VI); PG yalnız imzalı FORMU üretir + callback
/// imzasını doğrular + (3DModel) tamamlama çağırır. Opsiyonel yetenek → ayrı arayüz (port'u şişirmez).
/// </summary>
public interface IThreeDCapableAdapter
{
    /// <summary>Bankanın 3D gateway'ine gidecek imzalı form (PAN YOK — tarayıcı ekler). ActionUrl = banka 3D ucu.</summary>
    ThreeDForm Build3DForm(string orderRef, decimal amount, string currency, string okUrl, string failUrl);

    /// <summary>Banka callback'inin HASH'ini doğrular (HASHPARAMS sırası + SecretKey).</summary>
    bool VerifyCallback(IReadOnlyDictionary<string, string> callback);

    /// <summary>3DModel tamamlama: 3D doğrulandı → asıl çekim (NonSecure + MD). 3DPay/3DHost'ta gereksiz.</summary>
    Task<ResultDomain<BankChargeOutcome>> Complete3DAsync(string orderId, string md, CancellationToken ct);
}

/// <summary>PG hosted sayfasının render edeceği 3D form: banka ActionUrl + gizli alanlar (Hash dahil) + OrderId.</summary>
public sealed record ThreeDForm(string ActionUrl, IReadOnlyList<KeyValuePair<string, string>> HiddenFields, string OrderId);
