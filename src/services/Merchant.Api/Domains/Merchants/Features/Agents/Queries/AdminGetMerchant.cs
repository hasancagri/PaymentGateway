namespace Merchant.Api.Domains.Merchants.Features.Agents.Queries;

// 044 US1: admin tekil merchant detayı — hassas (Email/GsmNumber/IdentityNumber/Iban/TaxNumber)
// ve sır (MerchantKey/SubMerchantKey) alanlar SÖZLEŞMEDE HİÇ YOK (FR-002/FR-003 — boş string
// olarak bile dönmez; hassas yönetim tek BFF sayfasında). Agent slice kendi sorgusu (MerchantScoped
// GetMerchant REST query'siyle KOD PAYLAŞMAZ, bilinçli tekrar — conventions.md).
public static class AdminGetMerchant
{
    [RequiredScope(AuthorizationScopes.MerchantAdmin)]
    public record AdminGetMerchantQuery(Guid MerchantId);

    public class AdminGetMerchantResponse
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

    public class AdminGetMerchantQueryHandler
    {
        public async Task<FeatureObjectResultModel<AdminGetMerchantResponse>> Handle(
            AdminGetMerchantQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(query.MerchantId, ct);
            if (merchant is null || merchant.IsDeleted)
                return FeatureObjectResultModel<AdminGetMerchantResponse>.Error(new MessageItem
                {
                    Property = nameof(query.MerchantId),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            return FeatureObjectResultModel<AdminGetMerchantResponse>.Ok(new AdminGetMerchantResponse
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

/// <summary>US1 — tekil merchant detayı (hassas-dışı alanlar); hassas/sır alan YOK.</summary>
[McpServerToolType]
public static class AdminGetMerchantMcpTool
{
    [McpServerTool(Name = Shared.MerchantAdminTools.GetMerchant)]
    [Description("Tekil merchant detayını döner (statü, tip, ad, adres, iletişim adı, vergi dairesi, " +
                 "unvan). Kişisel/finansal veri ve sır alanları YANIT'TA YOK; onlar yalnız Admin " +
                 "hassas-veri sayfasından yönetilir.")]
    public static Task<FeatureObjectResultModel<AdminGetMerchant.AdminGetMerchantResponse>>
        AdminGetMerchantAsync(
            [Description("Sorgulanacak merchant kimliği")] Guid merchantId,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminGetMerchant.AdminGetMerchantResponse>>(
            new AdminGetMerchant.AdminGetMerchantQuery(merchantId), ct);
}