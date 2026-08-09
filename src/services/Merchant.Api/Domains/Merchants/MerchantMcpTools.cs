using System.ComponentModel;
using ModelContextProtocol.Server;
using Merchant.Api.Domains.Merchants.Features.Queries;

namespace Merchant.Api.Domains.Merchants;

// MCP tool ince sarmalayıcıdır ve yalnızca Features/Queries slice'ını IMessageBus ile çağırır
// (Payment.Api deseni). Ayrı McpTools/ klasörü YOK — aggregate klasörü altında durur.

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