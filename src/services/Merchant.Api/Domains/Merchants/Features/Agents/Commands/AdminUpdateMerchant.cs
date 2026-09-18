using System.ComponentModel;
using Common.Utils.Authorization;
using ModelContextProtocol.Server;

namespace Merchant.Api.Domains.Merchants.Features.Agents.Commands;

// 044 US1: admin hassas-dışı merchant güncelleme. Girdi/çıktı sözleşmesinde hassas alan
// (Email/GsmNumber/IdentityNumber/Iban/TaxNumber) HİÇ YOK; aggregate'in UpdateDetails'i tüm
// alanları tek imzada aldığından hassas alanlar MEVCUT değerlerden geçirilir (research R3 —
// kısmi yazma yok, invariant'lar aynen çalışır). Agent slice kendi handler'ını taşır (bilinçli tekrar).
public static class AdminUpdateMerchant
{
    [RequiredScope(AuthorizationScopes.MerchantAdmin)]
    public record AdminUpdateMerchantCommand(
        Guid MerchantId,
        string Type,
        string Name,
        string Address,
        string ContactName,
        string ContactSurname,
        string? TaxOffice,
        string? LegalCompanyTitle);

    public class AdminUpdateMerchantResponse
    {
        public Guid MerchantId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string ContactSurname { get; set; } = string.Empty;
        public string? TaxOffice { get; set; }
        public string? LegalCompanyTitle { get; set; }
        public DateTime CreatedTime { get; set; }
    }

    [Transactional]
    public class AdminUpdateMerchantCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminUpdateMerchantResponse>> Handle(
            AdminUpdateMerchantCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            if (!Enum.TryParse<MerchantType>(cmd.Type?.Trim(), ignoreCase: true, out var type))
                return FeatureObjectResultModel<AdminUpdateMerchantResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Type),
                    Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
                });

            var merchant = await session.LoadAsync<Merchant>(cmd.MerchantId, ct);
            if (merchant is null || merchant.IsDeleted)
                return FeatureObjectResultModel<AdminUpdateMerchantResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.MerchantId),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = merchant.UpdateDetails(
                type, cmd.Name, merchant.Email, merchant.GsmNumber, cmd.Address, merchant.Iban,
                cmd.ContactName, cmd.ContactSurname,
                merchant.IdentityNumber, cmd.TaxOffice, merchant.TaxNumber, cmd.LegalCompanyTitle);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<AdminUpdateMerchantResponse>.Error(result.Messages);

            session.Store(merchant);

            return FeatureObjectResultModel<AdminUpdateMerchantResponse>.Ok(new AdminUpdateMerchantResponse
            {
                MerchantId = merchant.Id,
                Status = merchant.Status.ToString(),
                Type = merchant.Type.ToString(),
                Name = merchant.Name,
                Address = merchant.Address,
                ContactName = merchant.ContactName,
                ContactSurname = merchant.ContactSurname,
                TaxOffice = merchant.TaxOffice,
                LegalCompanyTitle = merchant.LegalCompanyTitle,
                CreatedTime = merchant.CreatedTime
            });
        }
    }
}

/// <summary>US1 — merchant'ın hassas-dışı alanlarını günceller; hassas alan parametresi YOK.</summary>
[McpServerToolType]
public static class AdminUpdateMerchantMcpTool
{
    [McpServerTool(Name = Shared.MerchantAdminTools.UpdateMerchant)]
    [Description("Merchant'ın hassas-dışı alanlarını günceller (tip, ad, adres, iletişim adı/soyadı, " +
                 "vergi dairesi, unvan). Kişisel/finansal alanlar bu tool'dan DEĞİŞTİRİLEMEZ; onlar " +
                 "yalnız Admin hassas-veri sayfasından yönetilir. Tip: Personal | PrivateCompany | " +
                 "LimitedOrJointStockCompany.")]
    public static Task<FeatureObjectResultModel<AdminUpdateMerchant.AdminUpdateMerchantResponse>>
        AdminUpdateMerchantAsync(
            [Description("Güncellenecek merchant kimliği")] Guid merchantId,
            [Description("İşyeri tipi: Personal | PrivateCompany | LimitedOrJointStockCompany")] string type,
            [Description("İşyeri adı")] string name,
            [Description("Adres")] string address,
            [Description("İletişim adı")] string contactName,
            [Description("İletişim soyadı")] string contactSurname,
            IMessageBus bus,
            CancellationToken ct,
            [Description("Vergi dairesi (şirket tiplerinde zorunlu)")] string? taxOffice = null,
            [Description("Ticari unvan (şirket tiplerinde zorunlu)")] string? legalCompanyTitle = null)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminUpdateMerchant.AdminUpdateMerchantResponse>>(
            new AdminUpdateMerchant.AdminUpdateMerchantCommand(
                merchantId, type, name, address, contactName, contactSurname, taxOffice, legalCompanyTitle), ct);
}