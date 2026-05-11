namespace MerchantManagement.Api.Domains.Merchants.Features.Queries;

public static class GetMerchantById
{
    public class GetMerchantByIdQuery
    {
        public required Guid MerchantId { get; set; }
    }

    public class GetMerchantByIdResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public MerchantStatus Status { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Country { get; set; }
        public string City { get; set; }
        public string Mcc { get; set; }
        public string WebhookUrl { get; set; }
    }

    public class GetMerchantByIdHandler
    {
        public async Task<FeatureObjectResultModel<GetMerchantByIdResponse>> Handle(
            GetMerchantByIdQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(query.MerchantId, ct);
            if (merchant is null)
                return FeatureObjectResultModel<GetMerchantByIdResponse>.Error(new MessageItem
                {
                    Table = nameof(Merchant),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            return FeatureObjectResultModel<GetMerchantByIdResponse>.Ok(new GetMerchantByIdResponse
            {
                Id = merchant.Id,
                Name = merchant.Name.Value,
                Status = merchant.Status,
                Email = merchant.ContactInfo.Email,
                Phone = merchant.ContactInfo.Phone,
                Country = merchant.Address.Country,
                City = merchant.Address.City,
                Mcc = merchant.Mcc.Value,
                WebhookUrl = merchant.WebhookUrl.Value
            });
        }
    }
}