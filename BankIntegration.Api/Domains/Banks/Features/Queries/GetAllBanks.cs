namespace BankIntegration.Api.Domains.Banks.Features.Queries;

public static class GetAllBanks
{
    public class GetAllBanksQuery { }

    public class BankListItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Priority { get; set; }
        public string Status { get; set; }
    }

    public class GetAllBanksHandler
    {
        public async Task<FeatureObjectResultModel<List<BankListItem>>> Handle(
            GetAllBanksQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var banks = await session.Query<Bank>().ToListAsync(ct);
            var result = banks.Select(x => new BankListItem
            {
                Id = x.Id,
                Name = x.Name.Value,
                Priority = x.Priority.Value,
                Status = x.Status.ToString()
            }).ToList();

            return FeatureObjectResultModel<List<BankListItem>>.Ok(result);
        }
    }
}