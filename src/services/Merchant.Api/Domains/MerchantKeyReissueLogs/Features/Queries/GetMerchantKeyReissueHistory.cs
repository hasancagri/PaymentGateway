using Merchant.Api.Domains.MerchantKeyReissueLogs;

namespace Merchant.Api.Domains.MerchantKeyReissueLogs.Features.Queries;

// 046 US2/FR-007: key yenileme geçmişi — merchantId ile salt-append kayıt listesi (en yeni önce).
// MerchantScoped: merchant kendi token'ıyla yalnız kendi geçmişini okur (route {merchantId} eşleşmesi).
public static class GetMerchantKeyReissueHistory
{
    public record GetMerchantKeyReissueHistoryQuery(Guid MerchantId);

    public class ReissueHistoryItem
    {
        public Guid Id { get; set; }
        public DateTimeOffset ReissuedAt { get; set; }
        public string InitiatedBy { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }

    public class GetMerchantKeyReissueHistoryQueryHandler
    {
        public async Task<FeatureListResultModel<ReissueHistoryItem>> Handle(
            GetMerchantKeyReissueHistoryQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var items = await session.Query<MerchantKeyReissueLog>()
                .Where(l => l.MerchantId == query.MerchantId)
                .OrderByDescending(l => l.ReissuedAt)
                .ToListAsync(ct);

            return FeatureListResultModel<ReissueHistoryItem>.Ok(items.Select(l => new ReissueHistoryItem
            {
                Id = l.Id,
                ReissuedAt = l.ReissuedAt,
                InitiatedBy = l.InitiatedBy,
                Reason = l.Reason
            }).ToList());
        }
    }
}

public static class GetMerchantKeyReissueHistoryEndpoint
{
    public static RouteGroupBuilder GetMerchantKeyReissueHistoryGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{merchantId:guid}/key-reissue-history",
                async (Guid merchantId, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureListResultModel<GetMerchantKeyReissueHistory.ReissueHistoryItem>>(
                            new GetMerchantKeyReissueHistory.GetMerchantKeyReissueHistoryQuery(merchantId));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
                })
            .WithName("GetMerchantKeyReissueHistory")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantRead, AuthorizationPolicies.MerchantScoped)
            .Produces<List<GetMerchantKeyReissueHistory.ReissueHistoryItem>>();

        return group;
    }
}
