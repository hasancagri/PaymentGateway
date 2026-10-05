namespace Merchant.Api.Domains.Merchants.Features.Agents.Queries;

// D5: komisyon/merchant akışında çıplak GUID yerine ad/e-posta ile merchant bulma — agent iki adımda
// zincirler (önce bul, sonra id'yi komisyon tool'una besle). İç ResolveMerchantByName'in (reissue yolu,
// strict-exact tek-id) agent EŞİ ama arama semantiği: Name VEYA Email PARÇA eşleşmesi (ci), aday LİSTESİ
// döner (belirsizlik hata değil — agent seçer). MerchantKey/Email/PII yanıtta HİÇ YOK (FR-002/FR-003).
// Agent slice kendi sorgusu (bilinçli tekrar — conventions.md).
public static class AdminFindMerchant
{
    [RequiredScope(AuthorizationScopes.MerchantAdmin)]
    public record AdminFindMerchantQuery(string Query);

    public class AdminFindMerchantResponse
    {
        public List<MerchantItem> Matches { get; set; } = new();
    }

    public class MerchantItem
    {
        public Guid MerchantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    public class AdminFindMerchantQueryHandler
    {
        public async Task<FeatureObjectResultModel<AdminFindMerchantResponse>> Handle(
            AdminFindMerchantQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var term = query.Query?.Trim().ToLower();
            if (string.IsNullOrWhiteSpace(term))
                return FeatureObjectResultModel<AdminFindMerchantResponse>.Error(new MessageItem
                {
                    Property = nameof(query.Query),
                    Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
                });

            var matches = await session.Query<Merchant>()
                .Where(m => !m.IsDeleted
                    && (m.Name.ToLower().Contains(term) || m.Email.ToLower().Contains(term)))
                .OrderBy(m => m.Name)
                .Take(20)
                .ToListAsync(ct);

            return FeatureObjectResultModel<AdminFindMerchantResponse>.Ok(new AdminFindMerchantResponse
            {
                Matches = matches.Select(m => new MerchantItem
                {
                    MerchantId = m.Id,
                    Name = m.Name,
                    Status = m.Status.ToString(),
                    Type = m.Type.ToString()
                }).ToList()
            });
        }
    }
}

/// <summary>D5 — ad/e-posta parçasıyla merchant arar; aday listesi (id + ad + statü + tip), sır/PII YOK.</summary>
[McpServerToolType]
public static class AdminFindMerchantMcpTool
{
    [McpServerTool(Name = Shared.MerchantAdminTools.FindMerchant)]
    [Description(Shared.McpToolDescriptions.MerchantAdminTools.FindMerchant)]
    public static Task<FeatureObjectResultModel<AdminFindMerchant.AdminFindMerchantResponse>>
        AdminFindMerchantAsync(
            IMessageBus bus,
            CancellationToken ct,
            [Description("Aranacak ad ya da e-posta (parça eşleşir, büyük/küçük harf duyarsız)")] string query)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminFindMerchant.AdminFindMerchantResponse>>(
            new AdminFindMerchant.AdminFindMerchantQuery(query), ct);
}