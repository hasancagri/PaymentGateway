using System.ComponentModel;
using ModelContextProtocol.Server;
using Agent = Payment.Api.Domains.PaymentSessions.Features.Agent;

namespace Payment.Api.Domains.PaymentSessions;

// MCP tool'ları ince sarmalayıcıdır ve yalnızca Features/Agent slice'larını IMessageBus ile çağırır
// (ECommerce deseni). Agent'a açık her işlem Agent klasöründe görünür. Tutar/banka/kart LLM'de
// değil domain'de; tool yalnız girdiyi slice'a taşır. process_payment YOK (çekim ertelendi).

/// <summary>Faz 1 — token + sepet tutarından Model A taksit listesi üretir, oturumu açar.</summary>
[McpServerToolType]
public static class GetInstallmentOptionsMcpTool
{
    [McpServerTool(Name = "get_installment_options")]
    [Description("Kayıtlı kart token'ı ve sepet tutarından desteklenen taksit seçeneklerini (Model A: " +
                 "her satır tutarı = sepet tutarı, komisyon eklenmez) session-tabanlı üretir; sessionId döner. " +
                 "Tam kart bilgisi (PAN/CVV) KABUL ETMEZ.")]
    public static Task<FeatureObjectResultModel<Agent.QuoteInstallmentsForSession.QuoteInstallmentsForSessionResponse>>
        GetInstallmentOptionsAsync(
            [Description("Kayıtlı kart token'ı (PAN değil)")] string cardToken,
            [Description("Sepet tutarı (TL, nokta ondalık, > 0)")] decimal cartAmount,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<Agent.QuoteInstallmentsForSession.QuoteInstallmentsForSessionResponse>>(
            new Agent.QuoteInstallmentsForSession.QuoteInstallmentsForSessionCommand(cardToken, cartAmount), ct);
}

/// <summary>Faz 2 — kullanıcının seçtiği taksiti oturuma yazar. Çekim yapmaz.</summary>
[McpServerToolType]
public static class SelectInstallmentMcpTool
{
    [McpServerTool(Name = "select_installment")]
    [Description("Açık ödeme oturumunda, get_installment_options'ın sunduğu listeden bir taksiti seçer ve " +
                 "oturuma yazar (faz: taksit seçildi). Fiili çekim YAPMAZ.")]
    public static Task<FeatureObjectResultModel<Agent.SelectInstallment.SelectInstallmentResponse>>
        SelectInstallmentAsync(
            [Description("get_installment_options'ın döndüğü sessionId")] Guid sessionId,
            [Description("Sunulan listeden seçilen taksit sayısı")] int installmentCount,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<Agent.SelectInstallment.SelectInstallmentResponse>>(
            new Agent.SelectInstallment.SelectInstallmentCommand(sessionId, installmentCount), ct);
}

/// <summary>Story 3 — ödeme oturumunun güncel fazını döner.</summary>
[McpServerToolType]
public static class PaymentStatusMcpTool
{
    [McpServerTool(Name = "payment_status")]
    [Description("Ödeme oturumunun güncel durumunu (faz + seçilen taksit) döner.")]
    public static Task<FeatureObjectResultModel<Agent.GetPaymentSessionStatus.GetPaymentSessionStatusResponse>>
        PaymentStatusAsync(
            [Description("Sorgulanacak sessionId")] Guid sessionId,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<Agent.GetPaymentSessionStatus.GetPaymentSessionStatusResponse>>(
            new Agent.GetPaymentSessionStatus.GetPaymentSessionStatusQuery(sessionId), ct);
}