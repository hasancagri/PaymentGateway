namespace CommissionManagement.Api.Domains.BankCommissions.Features.Queries;

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
            IQuerySession session,
            CancellationToken ct)
        {
            var all = await session.Query<BankCommission>().ToListAsync(ct);
            var filtered = query.BankId.HasValue
                ? all.Where(x => x.BankId == query.BankId.Value)
                : all;

            var list = filtered.Select(x => new BankCommissionListItem
            {
                Id = x.Id,
                BankId = x.BankId,
                Rate = x.Rate.Value,
                CardBrand = x.Criteria.CardBrand,
                CardType = x.Criteria.CardType,
                TransactionRegion = x.Criteria.TransactionRegion
            }).ToList();

            return FeatureObjectResultModel<List<BankCommissionListItem>>.Ok(list);
        }
    }
}