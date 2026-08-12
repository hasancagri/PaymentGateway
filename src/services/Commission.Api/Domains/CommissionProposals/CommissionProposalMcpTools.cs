using AgentRevise = Commission.Api.Domains.CommissionDrafts.Features.Agents.ReviseCommissionDraftForAgent;
using AgentShow = Commission.Api.Domains.CommissionDrafts.Features.Agents.ShowCommissionDraftForAgent;
using AgentStatus = Commission.Api.Domains.CommissionProposals.Features.Agents.CommissionProposalStatusForAgent;
using AgentSubmit = Commission.Api.Domains.CommissionProposals.Features.Agents.SubmitCommissionProposalForAgent;

namespace Commission.Api.Domains.CommissionProposals;

// 019 — komisyon teklif/pazarlık MCP tool'ları. Tüketen = Merchant.Agent (commission.write).
// MCP tool ince sarmalayıcıdır ve YALNIZ Features/Agents slice'ını IMessageBus ile çağırır (015);
// Commands/Queries'e gitmez. Ayrı McpTools/ klasörü YOK — aggregate klasörü altında durur.

[McpServerToolType]
public static class SubmitCommissionProposalMcpTool
{
    [McpServerTool(Name = "submit_commission_proposal")]
    [Description("Merchant'a komisyon teklifi sunar veya revize edilmiş taslağı YENİDEN gönderir " +
                 "('merchant'a gönder'). Taslak yoksa banka grid'i + sabit marjdan üretir; önceki bekleyen " +
                 "teklif geçersiz olur. Excel ekli + kabul/ret linkli mail kuyruğa düşer. merchantEmail'i " +
                 "get_merchant tool'undan al; UYDURMA.")]
    public static Task<FeatureObjectResultModel<AgentSubmit.SubmitCommissionProposalResponse>>
        SubmitCommissionProposalAsync(
            [Description("Merchant kimliği (get_merchant'tan)")] Guid merchantId,
            [Description("Merchant iletişim e-postası (get_merchant'tan)")] string merchantEmail,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AgentSubmit.SubmitCommissionProposalResponse>>(
            new AgentSubmit.SubmitCommissionProposalCommand(merchantId, merchantEmail), ct);
}

[McpServerToolType]
public static class ReviseCommissionDraftMcpTool
{
    [McpServerTool(Name = "revise_commission_draft")]
    [Description("Komisyon taslağını yapılandırılmış işlemlerle revize eder ve uygulanan diff'i (satır, " +
                 "eski → yeni) döner. İşlem biçimleri: {op:'set', row:37, rate:1.85} | {op:'set', " +
                 "bank:'Akbank', installment:6, rate:1.8} | {op:'delta', filter:{installment:12}, " +
                 "delta:-0.2} | {op:'set', filter:{bank:'Akbank'}, rate:1.9}. Adresleme bu üç biçimden " +
                 "biri ZORUNLU: row (tek satır) | bank+installment (İKİSİ BİRLİKTE) | filter. Banka " +
                 "verilmeden 'tüm N taksitler' için filter:{installment:N} kullan — üst düzey " +
                 "installment'ı banka olmadan tek başına KOYMA. YALNIZ admin'in açıkça " +
                 "söylediği değerleri koy; hesap sunucuda yapılır. Banka oranının altına inen değişiklik " +
                 "BÜTÜN olarak reddedilir; merchant'a hiçbir şey gönderilmez.")]
    public static Task<FeatureObjectResultModel<AgentRevise.ReviseCommissionDraftResponse>>
        ReviseCommissionDraftAsync(
            [Description("Merchant kimliği (get_merchant'tan)")] Guid merchantId,
            [Description("Revizyon işlemleri (admin'in açık değerleri)")] List<AgentRevise.ReviseOperationInput> operations,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AgentRevise.ReviseCommissionDraftResponse>>(
            new AgentRevise.ReviseCommissionDraftCommand(merchantId, operations), ct);
}

[McpServerToolType]
public static class ShowCommissionDraftMcpTool
{
    [McpServerTool(Name = "show_commission_draft")]
    [Description("Komisyon taslağının güncel tam tablosunu döner: satır no, banka, kart markası/tipi, " +
                 "bölge, taksit, oran + kilit durumu (isLocked). 'Gönder'den önceki son kontrol. Read-only.")]
    public static Task<FeatureObjectResultModel<AgentShow.ShowCommissionDraftResponse>>
        ShowCommissionDraftAsync(
            [Description("Merchant kimliği (get_merchant'tan)")] Guid merchantId,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AgentShow.ShowCommissionDraftResponse>>(
            new AgentShow.ShowCommissionDraftQuery(merchantId), ct);
}

[McpServerToolType]
public static class CommissionProposalStatusMcpTool
{
    [McpServerTool(Name = "commission_proposal_status")]
    [Description("Merchant'ın SON komisyon teklifinin durumunu döner: None / Pending / Accepted / " +
                 "Rejected (+ ret gerekçesi + karar zamanı). Read-only.")]
    public static Task<FeatureObjectResultModel<AgentStatus.CommissionProposalStatusResponse>>
        CommissionProposalStatusAsync(
            [Description("Merchant kimliği (get_merchant'tan)")] Guid merchantId,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AgentStatus.CommissionProposalStatusResponse>>(
            new AgentStatus.CommissionProposalStatusQuery(merchantId), ct);
}