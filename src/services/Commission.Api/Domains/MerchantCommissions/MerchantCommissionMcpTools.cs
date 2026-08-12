using Agent = Commission.Api.Domains.MerchantCommissions.Features.Agents;

namespace Commission.Api.Domains.MerchantCommissions;

// 013 US4 — komisyon Excel orkestrasyonu (D14) grid kaynağı. Tüketen = harici LLM/MCP client
// (admin-düzlemi token; 019'da /mcp yüzeyi tek policy commission.write). Tool yalnız Features/Agents
// slice'ını IMessageBus ile sarar. 019: McpTools/ klasöründen aggregate köküne taşındı (015 kuralı).

[McpServerToolType]
public static class GetMerchantCommissionGridMcpTool
{
    [McpServerTool(Name = "get_merchant_commission_grid")]
    [Description("Merchant komisyon grid'ini düz tablo (columns + rows) olarak döner. Yalnız KABUL " +
                 "edilmiş teklifi olan merchant satır döner; aksi halde isEmpty:true, rows boş (Excel " +
                 "üretilmez). Read-only.")]
    public static Task<FeatureObjectResultModel<Agent.GetMerchantCommissionGrid.GetMerchantCommissionGridResponse>>
        GetMerchantCommissionGridAsync(
            [Description("Merchant kimliği")] Guid merchantId,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<Agent.GetMerchantCommissionGrid.GetMerchantCommissionGridResponse>>(
            new Agent.GetMerchantCommissionGrid.GetMerchantCommissionGridQuery(merchantId), ct);
}
