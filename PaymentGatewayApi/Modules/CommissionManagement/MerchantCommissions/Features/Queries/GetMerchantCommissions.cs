using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.Enums;

namespace PaymentGatewayApi.Modules.CommissionManagement.MerchantCommissions.Features.Queries;

public static class GetMerchantCommissions
{
    public class GetMerchantCommissionsQuery
    {
        public required Guid MerchantId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class MerchantCommissionListItem
    {
        public Guid Id { get; set; }
        public Guid BankCommissionId { get; set; }
        public decimal Rate { get; set; }
        public CardBrand CardBrand { get; set; }
        public CardType CardType { get; set; }
        public TransactionRegion TransactionRegion { get; set; }
    }

    public class GetMerchantCommissionsHandler
    {
        public async Task<FeatureObjectResultModel<List<MerchantCommissionListItem>>> Handle(
            GetMerchantCommissionsQuery query,
            CommissionManagementContext db,
            CancellationToken ct)
        {
            var list = await db.Set<MerchantCommission>()
                .Where(x => x.MerchantId == query.MerchantId)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(x => new MerchantCommissionListItem
                {
                    Id = x.Id,
                    BankCommissionId = x.BankCommissionId,
                    Rate = x.Rate.Value,
                    CardBrand = x.Criteria.CardBrand,
                    CardType = x.Criteria.CardType,
                    TransactionRegion = x.Criteria.TransactionRegion
                }).ToListAsync(ct);

            return FeatureObjectResultModel<List<MerchantCommissionListItem>>.Ok(list);
        }
    }
}