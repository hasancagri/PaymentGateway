using Agent = Merchant.Api.Domains.Merchants.Features.Agents;

namespace Merchant.Api.Domains.Merchants;

// MCP tool ince sarmalayıcıdır ve yalnızca Features/Agent slice'ını IMessageBus ile çağırır
// (Payment.Api deseni). Ayrı McpTools/ klasörü YOK — aggregate klasörü altında durur.
// Agent slice Commands/Queries'e dokunmaz; MCP de yalnız Agent slice'ını çağırır.

/// <summary>US4 — komisyon Excel orkestrasyonu (D14) ilk adımı: merchant iletişim/kimlik bilgisi.</summary>
[McpServerToolType]
public static class GetMerchantMcpTool
{
    [McpServerTool(Name = "get_merchant")]
    [Description("Merchant'ın kimlik/iletişim/statü bilgisini döner (id, ad, e-posta, durum). Komisyon " +
                 "Excel maili orkestrasyonunun ilk adımı. Read-only.")]
    public static Task<FeatureObjectResultModel<Agent.GetMerchantForAgent.GetMerchantForAgentResponse>>
        GetMerchantAsync(
            [Description("Merchant kimliği")] Guid merchantId,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<Agent.GetMerchantForAgent.GetMerchantForAgentResponse>>(
            new Agent.GetMerchantForAgent.GetMerchantForAgentQuery(merchantId), ct);
}