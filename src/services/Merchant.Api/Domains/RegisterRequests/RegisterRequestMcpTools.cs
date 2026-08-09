namespace Merchant.Api.Domains.RegisterRequests;

// MCP tool'ları ince sarmalayıcıdır ve yalnızca Features/Agent slice'larını IMessageBus ile çağırır
// (Payment.Api deseni). Ayrı McpTools/ klasörü YOK — iş süreçleri aggregate klasörü altında durur.
// Dışa kapalı: gateway Merchant.Agent istemcisi tüketir (merchant.write).

/// <summary>US1 — merchant adayı başvurusu (descriptor link; admin onayı bekler).</summary>
[McpServerToolType]
public static class SubmitRegistrationMcpTool
{
    [McpServerTool(Name = "submit_registration")]
    [Description("Merchant adayının başvuru alanlarından kayıt başvurusu başlatır (push-inline): alanlar " +
                 "doğrulanır ve başvuru doğrudan Pending (admin onayı bekler) olarak oluşturulur; RequestId " +
                 "döner (takip için). Kimlik/sır KABUL ETMEZ, merchant OLUŞTURMAZ.")]
    public static Task<FeatureObjectResultModel<SubmitRegistrationForAgent.SubmitRegistrationResponse>>
        SubmitRegistrationAsync(
            [Description("Aday alan adı (ör. shop.example.com)")] string domain,
            [Description("Yasal unvan")] string legalName,
            [Description("Vergi no")] string taxId,
            [Description("İletişim e-postası (geçerli e-posta)")] string contactEmail,
            [Description("Webhook adresi (mutlak HTTPS)")] string webhookUrl,
            IMessageBus bus,
            CancellationToken ct,
            [Description("Merchant'ın iletişim maili (başvuru bildirimi bu adrese gider; opsiyonel)")]
            string? merchantMail = null)
        => bus.InvokeAsync<FeatureObjectResultModel<SubmitRegistrationForAgent.SubmitRegistrationResponse>>(
            new SubmitRegistrationForAgent.SubmitRegistrationCommand(
                domain, legalName, taxId, contactEmail, webhookUrl, merchantMail), ct);
}

/// <summary>US1 (opsiyonel) — domain için başvuru durumu.</summary>
[McpServerToolType]
public static class RegistrationStatusMcpTool
{
    [McpServerTool(Name = "registration_status")]
    [Description("Verilen alan adı (domain) için başvurunun güncel durumunu döner: Pending / Approved / " +
                 "Rejected. Durum + sıradaki adım Message metniyle gelir (on-demand 'sürecim ne oldu?'; " +
                 "sürekli poll gerekmez).")]
    public static Task<FeatureObjectResultModel<RegistrationStatusForAgent.RegistrationStatusResponse>>
        RegistrationStatusAsync(
            [Description("Aday alan adı (ör. shop.example.com)")]
            string domain,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<RegistrationStatusForAgent.RegistrationStatusResponse>>(
            new RegistrationStatusForAgent.RegistrationStatusQuery(domain), ct);
}