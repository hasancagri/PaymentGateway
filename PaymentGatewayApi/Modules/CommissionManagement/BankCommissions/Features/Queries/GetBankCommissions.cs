using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.Enums;

namespace PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.Features.Queries;

public static class GetBankCommissions
{
    public class GetBankCommissionsQuery
    {
        public Guid? BankId { get; set; }
    }

    public class BankCommissionListItem
    {
        public Guid Id { get; set; }
        public Guid BankId { get; set; }
        public decimal Rate { get; set; }
        public CardBrand CardBrand { get; set; }
        public CardType CardType { get; set; }
        public TransactionRegion TransactionRegion { get; set; }
    }

    public class GetBankCommissionsHandler
    {
        public async Task<FeatureObjectResultModel<List<BankCommissionListItem>>> Handle(
            GetBankCommissionsQuery query,
            CommissionManagementContext db,
            CancellationToken ct)
        {
            var q = db.Set<BankCommission>().AsQueryable();
            if (query.BankId.HasValue)
                q = q.Where(x => x.BankId == query.BankId.Value);

            var list = await q.Select(x => new BankCommissionListItem
            {
                Id = x.Id,
                BankId = x.BankId,
                Rate = x.Rate.Value,
                CardBrand = x.Criteria.CardBrand,
                CardType = x.Criteria.CardType,
                TransactionRegion = x.Criteria.TransactionRegion
            }).ToListAsync(ct);

            return FeatureObjectResultModel<List<BankCommissionListItem>>.Ok(list);
        }
    }
}