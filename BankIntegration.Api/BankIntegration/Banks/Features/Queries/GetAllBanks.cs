namespace PaymentGatewayApi.Modules.BankIntegration.Banks.Features.Queries;

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
            BankIntegrationContext db,
            CancellationToken ct)
        {
            var banks = await db.Set<Bank>()
                .Select(x => new BankListItem
                {
                    Id = x.Id,
                    Name = x.Name.Value,
                    Priority = x.Priority.Value,
                    Status = x.Status.ToString()
                })
                .ToListAsync(ct);

            return FeatureObjectResultModel<List<BankListItem>>.Ok(banks);
        }
    }
}