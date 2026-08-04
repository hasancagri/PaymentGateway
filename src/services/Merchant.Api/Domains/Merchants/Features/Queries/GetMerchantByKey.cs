using Merchant.Api.Domains.Reference;

namespace Merchant.Api.Domains.Merchants.Features.Queries;

public static class GetMerchantByKey
{
    public record GetMerchantByKeyQuery(string MerchantKey);

    public class GetMerchantByKeyResponse
    {
        public Guid Id { get; set; }
        public string MerchantKey { get; set; } = string.Empty;
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

    public class GetMerchantByKeyQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetMerchantByKeyResponse>> Handle(
            GetMerchantByKeyQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(query.MerchantKey))
                return FeatureObjectResultModel<GetMerchantByKeyResponse>.NotFound();

            var merchant = await session.Query<Merchant>()
                .Where(m => m.MerchantKey == query.MerchantKey && !m.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (merchant is null)
                return FeatureObjectResultModel<GetMerchantByKeyResponse>.NotFound();

            // İsim zenginleştirme: yerel read-model'den (id = Code).
            var country = await session.LoadAsync<ReferenceCountry>(ReferenceKey.Country(merchant.CountryCode), ct);
            var city = await session.LoadAsync<ReferenceCity>(merchant.CityCode, ct);
            var mcc = await session.LoadAsync<ReferenceMcc>(merchant.Mcc, ct);

            return FeatureObjectResultModel<GetMerchantByKeyResponse>.Ok(new GetMerchantByKeyResponse
            {
                Id = merchant.Id,
                MerchantKey = merchant.MerchantKey,
                Name = merchant.Name,
                Email = merchant.Email,
                Phone = merchant.Phone,
                CountryCode = merchant.CountryCode,
                CountryName = country?.Name,
                CityCode = merchant.CityCode,
                CityName = city?.Name,
                Mcc = merchant.Mcc,
                MccName = mcc?.Name,
                WebhookUrl = merchant.WebhookUrl,
                Status = merchant.Status.ToString(),
                CreatedTime = merchant.CreatedTime
            });
        }
    }
}

public static class GetMerchantByKeyQueryEndpoint
{
    public static RouteGroupBuilder GetMerchantByKeyGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/by-key/{merchantKey}",
                async (string merchantKey, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<GetMerchantByKey.GetMerchantByKeyResponse>>(
                            new GetMerchantByKey.GetMerchantByKeyQuery(merchantKey));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
                })
            .WithName("GetMerchantByKey")
            .MapToApiVersion(1, 0)
            .Produces<GetMerchantByKey.GetMerchantByKeyResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}