using System.ComponentModel;
using ModelContextProtocol.Server;
using Agent = Commission.Api.Domains.MerchantCommissions.Features.Agent;

namespace Commission.Api.McpTools;

// 013 US4 — komisyon Excel orkestrasyonu (D14) grid kaynağı. Tüketen = harici LLM/MCP client
// (admin-düzlemi token, commission.read). Tool yalnız Features/Agent slice'ını IMessageBus ile sarar.

[McpServerToolType]
public static class GetMerchantCommissionGridMcpTool
{
    [McpServerTool(Name = "get_merchant_commission_grid")]
    [Description("Merchant komisyon grid'ini düz tablo (columns + rows) olarak döner. Yalnız Ready grid " +
                 "satır döner; Draft/tanımsız → isEmpty:true, rows boş (Excel üretilmez). Read-only.")]
    public static Task<FeatureObjectResultModel<Agent.GetMerchantCommissionGrid.GetMerchantCommissionGridResponse>>
        GetMerchantCommissionGridAsync(
            [Description("Merchant kimliği")] Guid merchantId,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<Agent.GetMerchantCommissionGrid.GetMerchantCommissionGridResponse>>(
            new Agent.GetMerchantCommissionGrid.GetMerchantCommissionGridQuery(merchantId), ct);
}
