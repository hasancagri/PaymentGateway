namespace MerchantManagement.Api.Domains.Merchants.Features.Queries;

public static class GetAllMerchants
{
    public class GetAllMerchantsQuery { }

    public class MerchantListItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public MerchantStatus Status { get; set; }
        public string Email { get; set; }
        public string Country { get; set; }
        public string Mcc { get; set; }
    }

    public class GetAllMerchantsHandler
    {
        public async Task<FeatureObjectResultModel<List<MerchantListItem>>> Handle(
            GetAllMerchantsQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var merchants = await session.Query<Merchant>().ToListAsync(ct);
            var list = merchants.Select(x => new MerchantListItem
            {
                Id = x.Id,
                Name = x.Name.Value,
                Status = x.Status,
                Email = x.ContactInfo.Email,
                Country = x.Address.Country,
                Mcc = x.Mcc.Value
            }).ToList();

            return FeatureObjectResultModel<List<MerchantListItem>>.Ok(list);
        }
    }
}