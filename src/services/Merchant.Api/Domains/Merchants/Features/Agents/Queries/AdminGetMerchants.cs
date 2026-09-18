namespace Merchant.Api.Domains.Merchants.Features.Agents.Queries;

// 043 US1/US3: admin merchant listesi — opsiyonel statü filtresi, YALNIZ Merchant aggregate'i
// sorgular (RegisterRequests'e karışmaz — plan clarify kararı). MerchantKey/SubMerchantKey/Iban
// yanıtta HİÇ YOK (FR-002 — sır hiçbir MCP yanıtında görünmez). 044 FR-003: Email + GsmNumber
// da sözleşmeden ÇIKARILDI (kişisel veri agent kanalından geçmez; hassas yönetim BFF sayfasında).
// Agent slice kendi sorgusu (bilinçli tekrar — conventions.md).
public static class AdminGetMerchants
{
    [RequiredScope(AuthorizationScopes.MerchantAdmin)]
    public record AdminGetMerchantsQuery(string? Status = null);

    public class AdminGetMerchantsResponse
    {
        public List<MerchantItem> Merchants { get; set; } = new();
    }

    public class MerchantItem
    {
        public Guid MerchantId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string ContactSurname { get; set; } = string.Empty;
    }

    public class AdminGetMerchantsQueryHandler
    {
        public async Task<FeatureObjectResultModel<AdminGetMerchantsResponse>> Handle(
            AdminGetMerchantsQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            MerchantStatus? statusFilter = null;
            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                if (!Enum.TryParse<MerchantStatus>(query.Status.Trim(), ignoreCase: true, out var parsed))
                    return FeatureObjectResultModel<AdminGetMerchantsResponse>.Error(new MessageItem
                    {
                        Property = nameof(query.Status),
                        Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
                    });
                statusFilter = parsed;
            }

            var merchants = await session.Query<Merchant>()
                .Where(m => !m.IsDeleted)
                .OrderByDescending(m => m.CreatedTime)
                .ToListAsync(ct);

            var filtered = statusFilter is null
                ? merchants
                : merchants.Where(m => m.Status == statusFilter).ToList();

            return FeatureObjectResultModel<AdminGetMerchantsResponse>.Ok(new AdminGetMerchantsResponse
            {
                Merchants = filtered.Select(m => new MerchantItem
                {
                    MerchantId = m.Id,
                    Status = m.Status.ToString(),
                    Type = m.Type.ToString(),
                    Name = m.Name,
                    Address = m.Address,
                    ContactName = m.ContactName,
                    ContactSurname = m.ContactSurname
                }).ToList()
            });
        }
    }
}

/// <summary>US1/US3 — merchant listesi, opsiyonel statü filtresiyle; hassas/sır alan YOK (044).</summary>
[McpServerToolType]
public static class AdminGetMerchantsMcpTool
{
    [McpServerTool(Name = Shared.MerchantAdminTools.GetMerchants)]
    [Description("Merchant listesini döner (opsiyonel statü filtresi: Active | Passive | Suspended, " +
                 "boşsa tümü). Kişisel/finansal veri ve sır alanları YANIT'TA YOK.")]
    public static Task<FeatureObjectResultModel<AdminGetMerchants.AdminGetMerchantsResponse>>
        AdminGetMerchantsAsync(
            IMessageBus bus,
            CancellationToken ct,
            [Description("Opsiyonel statü filtresi: Active | Passive | Suspended")] string? status = null)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminGetMerchants.AdminGetMerchantsResponse>>(
            new AdminGetMerchants.AdminGetMerchantsQuery(status), ct);
}
