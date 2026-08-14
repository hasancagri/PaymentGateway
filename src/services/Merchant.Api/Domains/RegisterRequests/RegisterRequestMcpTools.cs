using System.ComponentModel;
using Merchant.Api.Domains.RegisterRequests.Features.Agents;
using ModelContextProtocol.Server;

namespace Merchant.Api.Domains.RegisterRequests;

// 029: MCP tool'ları ince sarmalayıcıdır ve YALNIZ Features/Agents slice'larını IMessageBus ile
// çağırır (015/016). Tool adları DIŞ SÖZLEŞMEDİR — ECommerce ChatAgent allowlist'i bu adları
// bekler (submit_registration, registration_status); değiştirme. Yüzey: /mcp, merchant.write.

/// <summary>US1 — merchant kayıt başvurusu (023 alan seti; admin onayı bekler).</summary>
[McpServerToolType]
public static class SubmitRegistrationMcpTool
{
    [McpServerTool(Name = "submit_registration")]
    [Description("Merchant kayıt başvurusu açar: alanlar doğrulanır (tip-uyum matrisi, TR IBAN, " +
                 "e-posta) ve başvuru Pending (admin onayı bekler) olarak kaydolur; requestId döner. " +
                 "Kimlik/sır KABUL ETMEZ, merchant OLUŞTURMAZ. Aynı e-postayla bekleyen başvuru " +
                 "varken tekrar çağrılamaz.")]
    public static Task<FeatureObjectResultModel<SubmitRegistrationForAgent.SubmitRegistrationResponse>>
        SubmitRegistrationAsync(
            [Description("İşyeri tipi: Personal | PrivateCompany | LimitedOrJointStockCompany")] string type,
            [Description("İşyeri/site adı")] string name,
            [Description("İletişim e-postası — başvuru kimliği; durum sorgusu bu adresle yapılır")] string email,
            [Description("Telefon (GSM)")] string gsmNumber,
            [Description("Adres")] string address,
            [Description("TR IBAN (mod-97 doğrulanır)")] string iban,
            [Description("Yetkili adı")] string contactName,
            [Description("Yetkili soyadı")] string contactSurname,
            IMessageBus bus,
            CancellationToken ct,
            [Description("TCKN — Personal ve PrivateCompany için zorunlu")] string? identityNumber = null,
            [Description("Vergi dairesi — PrivateCompany ve LimitedOrJointStockCompany için zorunlu")] string? taxOffice = null,
            [Description("Vergi no — LimitedOrJointStockCompany için zorunlu")] string? taxNumber = null,
            [Description("Ticari unvan — PrivateCompany ve LimitedOrJointStockCompany için zorunlu")] string? legalCompanyTitle = null)
        => bus.InvokeAsync<FeatureObjectResultModel<SubmitRegistrationForAgent.SubmitRegistrationResponse>>(
            new SubmitRegistrationForAgent.SubmitRegistrationCommand(
                type, name, email, gsmNumber, address, iban, contactName, contactSurname,
                identityNumber, taxOffice, taxNumber, legalCompanyTitle), ct);
}

/// <summary>US3 — e-posta ile başvuru durumu; Approved'da MerchantId + MerchantKey teslimi.</summary>
[McpServerToolType]
public static class RegistrationStatusMcpTool
{
    [McpServerTool(Name = "registration_status")]
    [Description("Verilen e-posta için EN SON başvurunun durumunu döner: Pending / Approved / Rejected " +
                 "(nedenle). Approved yanıtı merchantId + merchantKey içerir — yönetici bu ikiliyi kendi " +
                 "panelindeki merchant kimlik formuna kaydeder. On-demand sorgu; sürekli poll gerekmez.")]
    public static Task<FeatureObjectResultModel<RegistrationStatusForAgent.RegistrationStatusResponse>>
        RegistrationStatusAsync(
            [Description("Başvuruda kullanılan e-posta (büyük/küçük harf duyarsız)")] string email,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<RegistrationStatusForAgent.RegistrationStatusResponse>>(
            new RegistrationStatusForAgent.RegistrationStatusQuery(email), ct);
}
