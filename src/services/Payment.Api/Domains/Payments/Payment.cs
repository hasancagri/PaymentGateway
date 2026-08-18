namespace Payment.Api.Domains.Payments;

/// <summary>
/// Bir kayıtlı-kart çekiminin kaydı (033). Gateway'in ilk çekim aggregate'i — iptal/iade + denetim
/// temeli. iyzico maliyeti (oransal + sabit, string — sağlayıcı yanıtından ham) saklanır; efektif
/// komisyon BU aggregate'te HESAPLANMAZ (BC izolasyonu — Commission BC'nin işi, event üzerinden).
/// Sağlayıcı (iyzico) tipleri buraya GİRMEZ; handler map'ler (sağlayıcı sınırı).
/// </summary>
public class Payment : AggregateRoot
{
    private Payment()
    {
    }

    /// <summary>Sağlayıcı işlem kimliği (iyzico PaymentId) — Cancel/Refund girdisi. Failed'da boş.</summary>
    public string ProviderPaymentId { get; private set; } = string.Empty;

    /// <summary>Sahip merchant (kiracı).</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>Çekimde kullanılan kayıtlı kart token'ı (032 vault).</summary>
    public string VaultToken { get; private set; } = string.Empty;

    /// <summary>
    /// 039: yapısal çekimin idempotency + retrieve anahtarı (Order.Api üretir, opak HMAC hex). Kiracı-içi
    /// tekil (unique partial index). Agent/eski charge yolunda null (bu yollar key taşımaz).
    /// </summary>
    public string? CorrelationKey { get; private set; }

    /// <summary>Sepet tutarı.</summary>
    public decimal Price { get; private set; }

    /// <summary>Taksitli toplam tutar (vade farkı dahil; tek çekimde = Price).</summary>
    public decimal PaidPrice { get; private set; }

    /// <summary>Taksit sayısı (1 = tek çekim).</summary>
    public int Installment { get; private set; }

    /// <summary>iyzico oransal maliyet (yanıttan, ham string).</summary>
    public string ProviderCommission { get; private set; } = string.Empty;

    /// <summary>iyzico sabit maliyet (yanıttan, ham string).</summary>
    public string ProviderFee { get; private set; } = string.Empty;

    public PaymentStatus Status { get; private set; }

    /// <summary>
    /// Başarılı çekim kaydı: sağlayıcı işlem kimliği + tutarlar + iyzico maliyeti; Success doğar.
    /// Zorunlu: merchantId, vaultToken, providerPaymentId.
    /// </summary>
    /// <remarks>Handler: ChargePaymentCommandHandler</remarks>
    public static ResultDomain<Payment> Succeeded(
        Guid merchantId,
        string vaultToken,
        decimal price,
        decimal paidPrice,
        int installment,
        string providerPaymentId,
        string providerCommission,
        string providerFee)
    {
        if (merchantId == Guid.Empty ||
            string.IsNullOrWhiteSpace(vaultToken) ||
            string.IsNullOrWhiteSpace(providerPaymentId))
        {
            return ResultDomain<Payment>.Error(new MessageItem
            {
                Property = nameof(providerPaymentId),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });
        }

        return ResultDomain<Payment>.Ok(new Payment
        {
            MerchantId = merchantId,
            VaultToken = vaultToken.Trim(),
            Price = price,
            PaidPrice = paidPrice,
            Installment = installment,
            ProviderPaymentId = providerPaymentId.Trim(),
            ProviderCommission = (providerCommission ?? string.Empty).Trim(),
            ProviderFee = (providerFee ?? string.Empty).Trim(),
            Status = PaymentStatus.Success
        });
    }

    /// <summary>
    /// Başarısız çekim kaydı: sağlayıcı işlem kimliği YOK; Failed doğar (denetim izi). Zorunlu:
    /// merchantId, vaultToken.
    /// </summary>
    /// <remarks>Handler: ChargePaymentCommandHandler</remarks>
    public static ResultDomain<Payment> Failed(
        Guid merchantId, string vaultToken, decimal price, int installment)
    {
        if (merchantId == Guid.Empty || string.IsNullOrWhiteSpace(vaultToken))
        {
            return ResultDomain<Payment>.Error(new MessageItem
            {
                Property = nameof(vaultToken),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });
        }

        return ResultDomain<Payment>.Ok(new Payment
        {
            MerchantId = merchantId,
            VaultToken = vaultToken.Trim(),
            Price = price,
            Installment = installment,
            Status = PaymentStatus.Failed
        });
    }

    /// <summary>
    /// 039 yapısal çekim: sağlayıcı çağrısından ÖNCE yazılan idempotency marker'ı. Charging doğar,
    /// correlationKey taşır (unique partial index). Retry bu kaydı bulup tekrar tahsil etmez (FR-012).
    /// Zorunlu: merchantId, vaultToken, correlationKey.
    /// </summary>
    /// <remarks>Handler: ChargePaymentCommandHandler</remarks>
    public static ResultDomain<Payment> Begin(
        Guid merchantId, string vaultToken, string correlationKey,
        decimal price, decimal paidPrice, int installment)
    {
        if (merchantId == Guid.Empty ||
            string.IsNullOrWhiteSpace(vaultToken) ||
            string.IsNullOrWhiteSpace(correlationKey))
        {
            return ResultDomain<Payment>.Error(new MessageItem
            {
                Property = nameof(correlationKey),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });
        }

        return ResultDomain<Payment>.Ok(new Payment
        {
            MerchantId = merchantId,
            VaultToken = vaultToken.Trim(),
            CorrelationKey = correlationKey.Trim(),
            Price = price,
            PaidPrice = paidPrice,
            Installment = installment,
            Status = PaymentStatus.Charging
        });
    }

    /// <summary>
    /// 039: Charging marker'ı Success'e taşır (iyzico başarılı) — sağlayıcı kimliği + maliyet. Yalnız
    /// Charging'den geçilir (terminal kayıt tekrar mutate edilmez). Zorunlu: providerPaymentId.
    /// </summary>
    /// <remarks>Handler: ChargePaymentCommandHandler</remarks>
    public ResultDomain Succeed(string providerPaymentId, string providerCommission, string providerFee)
    {
        if (Status != PaymentStatus.Charging || string.IsNullOrWhiteSpace(providerPaymentId))
        {
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(providerPaymentId),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR
            });
        }

        ProviderPaymentId = providerPaymentId.Trim();
        ProviderCommission = (providerCommission ?? string.Empty).Trim();
        ProviderFee = (providerFee ?? string.Empty).Trim();
        Status = PaymentStatus.Success;
        return ResultDomain.Ok();
    }

    /// <summary>
    /// 039: Charging marker'ı Failed'e taşır (iyzico başarısız/hata) — denetim izi kalır (kayıt silinmez).
    /// Yalnız Charging'den geçilir.
    /// </summary>
    /// <remarks>Handler: ChargePaymentCommandHandler</remarks>
    public ResultDomain Fail()
    {
        if (Status != PaymentStatus.Charging)
        {
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(Status),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR
            });
        }

        Status = PaymentStatus.Failed;
        return ResultDomain.Ok();
    }
}

/// <summary>
/// Çekim sonucu (033). Cancel/Refund statüleri ayrı işte gelir (bu kayıt onların temeli).
/// </summary>
public enum PaymentStatus
{
    Success = 1,
    Failed = 2,
    // 039: yapısal çekim marker'ı — iyzico çağrısından önce persist; başarıda Success, hatada Failed.
    Charging = 3
}
