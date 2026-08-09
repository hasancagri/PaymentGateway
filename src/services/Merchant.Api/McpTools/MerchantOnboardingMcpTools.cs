using System.ComponentModel;
using ModelContextProtocol.Server;
using Merchant.Api.Domains.Merchants.Features.Queries;
using Agent = Merchant.Api.Domains.RegisterRequests.Features.Agent;

namespace Merchant.Api.McpTools;

// MCP tool'ları ince sarmalayıcıdır ve yalnızca Features/Agent slice'larını IMessageBus ile çağırır
// (Payment.Api deseni). Dışa kapalı: gateway Merchant.Agent istemcisi tüketir (merchant.write).

/// <summary>US1 — merchant adayı başvurusu (descriptor link + challenge doğrulama).</summary>
[McpServerToolType]
public static class SubmitRegistrationMcpTool
{
    [McpServerTool(Name = "submit_registration")]
    [Description("Merchant adayının verdiği descriptor linkinden kayıt başvurusu başlatır: descriptor " +
                 "okunur/doğrulanır, alan adı sahipliği domain-control challenge ile doğrulanır. Talep " +
                 "challenge geçmeden AwaitingDomainControl olarak doğar ve RequestId döner (takip için); " +
                 "kanıt geçince Pending'e ilerler. Henüz yayınlanmadıysa yayınlanacak değer/yol döner. " +
                 "Kimlik/sır KABUL ETMEZ, merchant OLUŞTURMAZ.")]
    public static Task<FeatureObjectResultModel<Agent.SubmitRegistration.SubmitRegistrationResponse>>
        SubmitRegistrationAsync(
            [Description("Adayın descriptor dosyasının tam linki (ör. https://shop/.well-known/merchant-descriptor.json)")]
            string descriptorUrl,
            IMessageBus bus,
            CancellationToken ct,
            [Description("Opsiyonel opak dış referans (aynen saklanır/döner)")] string? externalRef = null)
        => bus.InvokeAsync<FeatureObjectResultModel<Agent.SubmitRegistration.SubmitRegistrationResponse>>(
            new Agent.SubmitRegistration.SubmitRegistrationCommand(descriptorUrl, externalRef), ct);
}

/// <summary>US4 — komisyon Excel orkestrasyonu (D14) ilk adımı: merchant iletişim/kimlik bilgisi.</summary>
[McpServerToolType]
public static class GetMerchantMcpTool
{
    [McpServerTool(Name = "get_merchant")]
    [Description("Merchant'ın kimlik/iletişim/statü bilgisini döner (id, ad, e-posta, durum). Komisyon " +
                 "Excel maili orkestrasyonunun ilk adımı. Read-only.")]
    public static Task<FeatureObjectResultModel<GetMerchant.GetMerchantResponse>>
        GetMerchantAsync(
            [Description("Merchant kimliği")] Guid merchantId,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<GetMerchant.GetMerchantResponse>>(
            new GetMerchant.GetMerchantQuery(merchantId), ct);
}

/// <summary>US1 (opsiyonel) — domain için başvuru durumu.</summary>
[McpServerToolType]
public static class RegistrationStatusMcpTool
{
    [McpServerTool(Name = "registration_status")]
    [Description("Verilen alan adı (domain) için başvurunun güncel durumunu döner: AwaitingDomainControl / " +
                 "Pending / Approved / Rejected. Durum + sıradaki adım Message metniyle gelir (on-demand " +
                 "'sürecim ne oldu?'; sürekli poll gerekmez).")]
    public static Task<FeatureObjectResultModel<Agent.RegistrationStatus.RegistrationStatusResponse>>
        RegistrationStatusAsync(
            [Description("Aday alan adı (ör. shop.example.com)")] string domain,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<Agent.RegistrationStatus.RegistrationStatusResponse>>(
            new Agent.RegistrationStatus.RegistrationStatusQuery(domain), ct);
}
