
namespace Commission.Api.Domains.MerchantCommissions.Features.Queries;

/// <summary>
/// Bir merchant'ın grid satırlarını enriched döner: her kombinasyon için merchant oranı +
/// banka oran aralığı (min/max) + tavan-altı işareti (read-time; saklanmaz → banka oranı
/// değişince otomatik tazelenir). Satır kümesi = merchant kriterleri ∪ banka-servisli kriterler.
/// </summary>
public static class GetMerchantCommissions
{
    public record GetMerchantCommissionsQuery(Guid MerchantId);

    public class GetMerchantCommissionsResponse
    {
        public List<MerchantCommissionItem> Items { get; set; } = new();
    }

    public class MerchantCommissionItem
    {
        /// <summary>Merchant oranı varsa kaydın Id'si; yoksa null.</summary>
        public Guid? Id { get; set; }
        public Guid MerchantId { get; set; }
        public CriteriaView Criteria { get; set; } = new();

        /// <summary>Merchant oranı; girilmemişse null.</summary>
        public decimal? Rate { get; set; }

        /// <summary>Bu kombinasyonu servisleyen banka oranlarının min'i; banka yoksa null.</summary>
        public decimal? BankMin { get; set; }

        /// <summary>Aynı kümenin max'ı; banka yoksa null.</summary>
        public decimal? BankMax { get; set; }

        /// <summary>rate != null && bankMax != null && rate &lt;= bankMax.</summary>
        public bool BelowBankCeiling { get; set; }

        /// <summary>rate == null (merchant oranı henüz girilmemiş).</summary>
        public bool IsMissing { get; set; }
    }

    public class CriteriaView
    {
        public string CardBrand { get; set; } = string.Empty;
        public string CardType { get; set; } = string.Empty;
        public string TransactionRegion { get; set; } = string.Empty;
        public int InstallmentCount { get; set; }
    }

    /// <summary>
    /// Tavan-altı işareti (read-time, saf): merchant oranı girilmiş VE banka tavanı (max) var VE
    /// oran tavanın altında/eşitse true. Banka yoksa (bankMax == null) işaretsiz.
    /// </summary>
    public static bool ComputeBelowBankCeiling(decimal? rate, decimal? bankMax) =>
        rate != null && bankMax != null && rate <= bankMax;

    public class GetMerchantCommissionsQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetMerchantCommissionsResponse>> Handle(
            GetMerchantCommissionsQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            // Merchant komisyonları (düz MerchantId filtresi; başka merchant sızmaz — SC-004).
            var merchantCommissions = await session.Query<MerchantCommission>()
                .Where(c => c.MerchantId == query.MerchantId && !c.IsDeleted)
                .ToListAsync(ct);

            // Tüm banka komisyonları belleğe (aralık hesabı için); kombinasyona göre min/max.
            var bankCommissions = await session.Query<BankCommission>()
                .Where(c => !c.IsDeleted)
                .ToListAsync(ct);

            var bankByCriteria = bankCommissions
                .GroupBy(b => b.Criteria)
                .ToDictionary(
                    g => g.Key,
                    g => (Min: g.Min(x => x.Rate), Max: g.Max(x => x.Rate)));

            var merchantByCriteria = merchantCommissions
                .ToDictionary(c => c.Criteria);

            // Satır kümesi: merchant kriterleri ∪ banka-servisli kriterler.
            var allCriteria = new HashSet<Criteria>(merchantByCriteria.Keys);
            allCriteria.UnionWith(bankByCriteria.Keys);

            var items = allCriteria.Select(criteria =>
            {
                merchantByCriteria.TryGetValue(criteria, out var mc);
                var hasBank = bankByCriteria.TryGetValue(criteria, out var bank);

                decimal? rate = mc?.Rate;
                decimal? bankMin = hasBank ? bank.Min : null;
                decimal? bankMax = hasBank ? bank.Max : null;

                return new MerchantCommissionItem
                {
                    Id = mc?.Id,
                    MerchantId = query.MerchantId,
                    Rate = rate,
                    BankMin = bankMin,
                    BankMax = bankMax,
                    BelowBankCeiling = ComputeBelowBankCeiling(rate, bankMax),
                    IsMissing = rate == null,
                    Criteria = new CriteriaView
                    {
                        CardBrand = criteria.CardBrand.ToString(),
                        CardType = criteria.CardType.ToString(),
                        TransactionRegion = criteria.TransactionRegion.ToString(),
                        InstallmentCount = criteria.InstallmentCount
                    }
                };
            }).ToList();

            return FeatureObjectResultModel<GetMerchantCommissionsResponse>.Ok(new GetMerchantCommissionsResponse
            {
                Items = items
            });
        }
    }
}

public static class GetMerchantCommissionsQueryEndpoint
{
    public static RouteGroupBuilder GetMerchantCommissionsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/",
                async ([FromQuery] Guid merchantId, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<GetMerchantCommissions.GetMerchantCommissionsResponse>>(
                            new GetMerchantCommissions.GetMerchantCommissionsQuery(merchantId));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("GetMerchantCommissions")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.CommissionRead)
            .Produces<GetMerchantCommissions.GetMerchantCommissionsResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}