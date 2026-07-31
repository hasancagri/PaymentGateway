using Merchant.Api.Domains.Merchants.Lookups;

namespace Merchant.Api.Domains.Merchants.Features.Queries;

public static class GetMerchant
{
    public record GetMerchantQuery(Guid Id);

    public class GetMerchantResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string CountryCode { get; set; } = string.Empty;
        public string? CountryName { get; set; }
        public string CityCode { get; set; } = string.Empty;
        public string? CityName { get; set; }
        public string Mcc { get; set; } = string.Empty;
        public string? MccName { get; set; }
        public string WebhookUrl { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; }
    }

    public class GetMerchantQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetMerchantResponse>> Handle(
            GetMerchantQuery query,
            IDocumentSession session,
            ICountryLookup countryLookup,
            ICityLookup cityLookup,
            IMccLookup mccLookup,
            CancellationToken ct)
        {
            var merchant = await session.Query<Merchant>()
                .Where(m => m.Id == query.Id && !m.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (merchant is null)
                return FeatureObjectResultModel<GetMerchantResponse>.NotFound();

            return FeatureObjectResultModel<GetMerchantResponse>.Ok(new GetMerchantResponse
            {
                Id = merchant.Id,
                Name = merchant.Name,
                Email = merchant.Email,
                Phone = merchant.Phone,
                CountryCode = merchant.CountryCode,
                CountryName = countryLookup.NameOf(merchant.CountryCode),
                CityCode = merchant.CityCode,
                CityName = cityLookup.NameOf(merchant.CityCode),
                Mcc = merchant.Mcc,
                MccName = mccLookup.NameOf(merchant.Mcc),
                WebhookUrl = merchant.WebhookUrl,
                Status = merchant.Status.ToString(),
                CreatedTime = merchant.CreatedTime
            });
        }
    }
}

public static class GetMerchantQueryEndpoint
{
    public static RouteGroupBuilder GetMerchantGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}",
                async (Guid id, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<GetMerchant.GetMerchantResponse>>(
                            new GetMerchant.GetMerchantQuery(id));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
                })
            .WithName("GetMerchant")
            .MapToApiVersion(1, 0)
            .Produces<GetMerchant.GetMerchantResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}