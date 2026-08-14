namespace Shared;

public static class IntegrationEvents
{
    // Ödeme sonucu event'leri: Payment yayınlar; Order BC geldiğinde tüketecek.
    public record PaymentCompletedEvent(Guid PaymentId, string OrderNumber, decimal Amount, string BankCode);

    public record PaymentFailedEvent(Guid PaymentId, string OrderNumber, decimal Amount, string? Reason);

    // 033: kayıtlı kartla çekim tamamlandı — Payment.Api yayınlar; iyzico maliyetini (oransal + sabit,
    // string — Commission.CalculateEffectiveCommission girdi imzasıyla uyumlu) taşır. Efektif komisyon
    // Commission BC'nin işi (BC izolasyonu); bu event tüketici bağlantı noktası (şimdilik tüketici YOK).
    public record PaymentChargedEvent(Guid PaymentId, Guid MerchantId, decimal Price, decimal PaidPrice,
        int Installment, string IyzicoCommission, string IyzicoFee, string ProviderPaymentId);

    // Merchant yaşam döngüsü: Merchant.Api yayınlar; Identity.Server tüketir (OpenIddict istemci upsert).
    // Status/NewStatus ∈ { "Active", "Passive", "Suspended" } (string — BC enum'u Shared'a sızmaz).
    // MerchantKey yalnız Created'da taşınır (istemci sırrı); StatusChanged sır taşımaz.
    public record MerchantCreated(Guid MerchantId, string MerchantKey, string Status);

    public record MerchantStatusChanged(Guid MerchantId, string NewStatus);

    // 013: aktivasyon anında (key teslim) yayınlanır — Identity.Server tüketir (OpenIddict istemci
    // provision: Provisioning demeti). Onboarding'de MerchantCreated'ın yerini alır; MerchantKey
    // (istemci sırrı) yalnız burada taşınır, yalnız Identity'ye gider. Status = "Provisioning".
    public record MerchantProvisioned(Guid MerchantId, string MerchantKey, string Status);

    // 013: Commission.Api grid'i finalize edip Ready olunca yayınlar; Merchant.Api tüketir
    // (Active koşulu #2). Sır taşımaz; tek-yön (ready olunca ready kalır).
    public record MerchantCommissionGridReady(Guid MerchantId);

    // 016: deterministik mail gönderimi — BC handler'ı [Transactional] outbox ile yayınlar, Mail.Worker
    // tüketip SMTP ile gönderir (MCP DEĞİL — yalnız Agent MCP çağırır). Generic (domain sızmaz).
    // 019: opsiyonel generic tablo eki — Mail.Worker ClosedXML ile .xlsx üretip mail'e ekler.
    public record SendEmailRequested(string To, string Subject, string Body, bool IsHtml = false,
        EmailAttachmentTable? Attachment = null);

    // 019: xlsx'e dönüşecek generic tablo (domain bilgisi taşımaz; Headers + string satırlar).
    public record EmailAttachmentTable(string FileName, string[] Headers, string[][] Rows);
}