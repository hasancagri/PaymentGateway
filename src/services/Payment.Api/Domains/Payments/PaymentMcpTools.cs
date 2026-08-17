using System.ComponentModel;
using ModelContextProtocol.Server;
using Payment.Api.Domains.Payments.Features.Agents;

namespace Payment.Api.Domains.Payments;

// 038: MCP tool'ları ince sarmalayıcıdır ve YALNIZ Features/Agents slice'larını IMessageBus ile
// çağırır (015/016). Tool adları DIŞ SÖZLEŞMEDİR — Payment.Agent RouterInstructions bu adları
// bekler (get_installment_options, charge_saved_card); değiştirme. Yüzey: /mcp, payment.write;
// TEK tüketici Payment.Agent (makine token'ı).

/// <summary>US1 — kayıtlı kart (vault token) + tutar için taksit seçenekleri (READ-ONLY).</summary>
[McpServerToolType]
public static class InstallmentOptionsMcpTool
{
    [McpServerTool(Name = "get_installment_options")]
    [Description("Kayıtlı kartın vault token'ı + sepet tutarı ile iyzico taksit seçeneklerini döner " +
                 "(installmentNumber + o taksidin toplam tutarı; 1 = tek çekim). READ-ONLY — çekim " +
                 "YAPMAZ. PAN/CVC/sağlayıcı sırrı kabul etmez ve dönmez.")]
    public static Task<FeatureObjectResultModel<InstallmentOptionsForAgent.InstallmentOptionsView>>
        GetInstallmentOptionsAsync(
            [Description("Merchant (site) kimliği — Guid")] Guid merchantId,
            [Description("Kayıtlı kartın opak vault token'ı")] string vaultToken,
            [Description("Sepet toplamı (TL)")] decimal amount,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<InstallmentOptionsForAgent.InstallmentOptionsView>>(
            new InstallmentOptionsForAgent.InstallmentOptionsForAgentQuery(merchantId, vaultToken, amount), ct);
}

/// <summary>US2 — kayıtlı karttan GERÇEK çekim (statü kapılı; buyer gerçek müşteri, sepet sentetik).</summary>
[McpServerToolType]
public static class ChargeSavedCardMcpTool
{
    [McpServerTool(Name = "charge_saved_card")]
    [Description("Kayıtlı karttan GERÇEK ödeme çeker. Çağıran taraf kullanıcı onayını ALMIŞ olmalı. " +
                 "amount=sepet toplamı; paidPrice=seçilen taksidin toplam tutarı (taksit sorgusundan; " +
                 "tek çekimde amount ile aynı); installment=taksit sayısı (1=tek çekim); buyer=gerçek " +
                 "müşteri bilgisi (ECommerce toplar). Sepet kalemi GÖNDERİLMEZ — gateway sentezler. " +
                 "Merchant Active değilse fail-closed reddedilir. PAN/CVC kabul etmez ve dönmez.")]
    public static Task<FeatureObjectResultModel<ChargeSavedCardForAgent.ChargeResultView>>
        ChargeSavedCardAsync(
            [Description("Merchant (site) kimliği — Guid")] Guid merchantId,
            [Description("Kayıtlı kartın opak vault token'ı")] string vaultToken,
            [Description("Sepet toplamı (TL)")] decimal amount,
            [Description("Seçilen taksidin toplam ödenecek tutarı (TL)")] decimal paidPrice,
            [Description("Taksit sayısı (1 = tek çekim)")] int installment,
            [Description("Alıcı adı")] string buyerName,
            [Description("Alıcı soyadı")] string buyerSurname,
            [Description("Alıcı e-postası")] string buyerEmail,
            [Description("Alıcı GSM")] string buyerGsmNumber,
            [Description("Alıcı TCKN (11 hane)")] string buyerIdentityNumber,
            [Description("Alıcı adresi (kayıtlı adres)")] string buyerRegistrationAddress,
            [Description("Alıcı şehri")] string buyerCity,
            [Description("Alıcı ülkesi")] string buyerCountry,
            [Description("Alıcı IP'si")] string buyerIp,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<ChargeSavedCardForAgent.ChargeResultView>>(
            new ChargeSavedCardForAgent.ChargeSavedCardForAgentCommand(
                merchantId, vaultToken, amount, paidPrice, installment,
                new ChargeSavedCardForAgent.BuyerInput(
                    buyerName, buyerSurname, buyerEmail, buyerGsmNumber, buyerIdentityNumber,
                    buyerRegistrationAddress, buyerCity, buyerCountry, buyerIp)), ct);
}