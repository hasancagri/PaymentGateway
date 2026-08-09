using System.ComponentModel;
using ModelContextProtocol.Server;
using Agent = Merchant.Api.Domains.RegisterRequests.Features.Agent;

namespace Merchant.Api.Domains.RegisterRequests;

// MCP tool'ları ince sarmalayıcıdır ve yalnızca Features/Agent slice'larını IMessageBus ile çağırır
// (Payment.Api deseni). Ayrı McpTools/ klasörü YOK — iş süreçleri aggregate klasörü altında durur.
// Dışa kapalı: gateway Merchant.Agent istemcisi tüketir (merchant.write).

/// <summary>US1 — merchant adayı başvurusu (descriptor link; admin onayı bekler).</summary>
[McpServerToolType]
public static class SubmitRegistrationMcpTool
{
    [McpServerTool(Name = "submit_registration")]
    [Description("Merchant adayının verdiği descriptor linkinden kayıt başvurusu başlatır: descriptor " +
                 "okunur/doğrulanır ve başvuru doğrudan Pending (admin onayı bekler) olarak oluşturulur; " +
                 "RequestId döner (takip için). Kimlik/sır KABUL ETMEZ, merchant OLUŞTURMAZ.")]
    public static Task<FeatureObjectResultModel<Agent.SubmitRegistrationForAgent.SubmitRegistrationResponse>>
        SubmitRegistrationAsync(
            [Description("Adayın descriptor dosyasının tam linki (ör. https://shop/.well-known/merchant-descriptor.json)")]
            string descriptorUrl,
            IMessageBus bus,
            CancellationToken ct,
            [Description("Opsiyonel opak dış referans (aynen saklanır/döner)")] string? externalRef = null)
        => bus.InvokeAsync<FeatureObjectResultModel<Agent.SubmitRegistrationForAgent.SubmitRegistrationResponse>>(
            new Agent.SubmitRegistrationForAgent.SubmitRegistrationCommand(descriptorUrl, externalRef), ct);
}

/// <summary>US1 (opsiyonel) — domain için başvuru durumu.</summary>
[McpServerToolType]
public static class RegistrationStatusMcpTool
{
    [McpServerTool(Name = "registration_status")]
    [Description("Verilen alan adı (domain) için başvurunun güncel durumunu döner: Pending / Approved / " +
                 "Rejected. Durum + sıradaki adım Message metniyle gelir (on-demand 'sürecim ne oldu?'; " +
                 "sürekli poll gerekmez).")]
    public static Task<FeatureObjectResultModel<Agent.RegistrationStatusForAgent.RegistrationStatusResponse>>
        RegistrationStatusAsync(
            [Description("Aday alan adı (ör. shop.example.com)")] string domain,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<Agent.RegistrationStatusForAgent.RegistrationStatusResponse>>(
            new Agent.RegistrationStatusForAgent.RegistrationStatusQuery(domain), ct);
}