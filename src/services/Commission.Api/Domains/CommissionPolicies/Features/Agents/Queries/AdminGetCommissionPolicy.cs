namespace Commission.Api.Domains.CommissionPolicies.Features.Agents.Queries;

// 043 US4: salt-okuma komisyon sorgusu — Commission.Api'nin İLK MCP tool'u. Mevcut
// Features/Queries/GetCommissionPolicy.cs'in Marten sorgusunu agent slice'ı kendi kopyasıyla
// çalıştırır (bilinçli tekrar, conventions.md). Endpoint zaten CommissionRead istediği için ek
// [RequiredScope]/middleware GEREKMEZ (ecommerce-onboarding'de commission scope hiç yok —
// research.md #9). Politika yoksa hata DEĞİL, "tanımlı politika yok" bilgi mesajı (US4 AS2).
public static class AdminGetCommissionPolicy
{
    public record AdminGetCommissionPolicyQuery(Guid MerchantId);

    public class AdminGetCommissionPolicyResponse
    {
        public Guid MerchantId { get; set; }
        public bool HasPolicy { get; set; }
        public string? MarginSummary { get; set; }
        public string? Status { get; set; }
    }

    public class AdminGetCommissionPolicyQueryHandler
    {
        public async Task<FeatureObjectResultModel<AdminGetCommissionPolicyResponse>> Handle(
            AdminGetCommissionPolicyQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var policy = await session.Query<CommissionPolicy>()
                .Where(p => p.MerchantId == query.MerchantId && !p.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (policy is null)
                return FeatureObjectResultModel<AdminGetCommissionPolicyResponse>.Ok(
                    new AdminGetCommissionPolicyResponse
                    {
                        MerchantId = query.MerchantId,
                        HasPolicy = false,
                        MarginSummary = "Tanımlı politika yok"
                    });

            var firstTier = policy.Margin.Tiers[0];
            var marginSummary = policy.Margin.Tiers.Count == 1
                ? $"%{firstTier.RatePercent * 100:0.##} + {firstTier.FixedFee:0.00} TL"
                : $"%{firstTier.RatePercent * 100:0.##} + {firstTier.FixedFee:0.00} TL, " +
                  $"{policy.Margin.Tiers.Count} kademe";

            return FeatureObjectResultModel<AdminGetCommissionPolicyResponse>.Ok(
                new AdminGetCommissionPolicyResponse
                {
                    MerchantId = policy.MerchantId,
                    HasPolicy = true,
                    MarginSummary = marginSummary,
                    Status = policy.Status.ToString()
                });
        }
    }
}

/// <summary>US4 — merchant'ın komisyon politikasını (marj özeti) döner; create/update MCP'de YOK.</summary>
[McpServerToolType]
public static class AdminGetCommissionPolicyMcpTool
{
    [McpServerTool(Name = Shared.CommissionAdminTools.GetCommissionPolicy)]
    [Description("Merchant'ın marj politikasını özetler (ör. \"%2.5 + 0.10 TL, 3 kademe\"). Politika " +
                 "tanımlı değilse hata DÖNMEZ, hasPolicy=false + \"Tanımlı politika yok\" döner. " +
                 "Politika OLUŞTURMAZ/GÜNCELLEMEZ (salt-okuma; create/update Admin ekranından yürür).")]
    public static Task<FeatureObjectResultModel<AdminGetCommissionPolicy.AdminGetCommissionPolicyResponse>>
        AdminGetCommissionPolicyAsync(
            [Description("Sorgulanacak merchant kimliği")] Guid merchantId,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminGetCommissionPolicy.AdminGetCommissionPolicyResponse>>(
            new AdminGetCommissionPolicy.AdminGetCommissionPolicyQuery(merchantId), ct);
}
