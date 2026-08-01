namespace Merchant.Api.Domains.Merchants.Features.Queries;

public static class GetAllMerchants
{
    public record GetAllMerchantsQuery;

    public class GetAllMerchantsResponse
    {
        public List<MerchantItem> Merchants { get; set; } = new();
    }

    public class MerchantItem
    {
        public Guid Id { get; set; }
        public string MerchantKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Mcc { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class GetAllMerchantsQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetAllMerchantsResponse>> Handle(
            GetAllMerchantsQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var merchants = await session.Query<Merchant>()
                .Where(m => !m.IsDeleted)
                .ToListAsync(ct);

            return FeatureObjectResultModel<GetAllMerchantsResponse>.Ok(new GetAllMerchantsResponse
            {
                Merchants = merchants.Select(m => new MerchantItem
                {
                    Id = m.Id,
                    MerchantKey = m.MerchantKey,
                    Name = m.Name,
                    Mcc = m.Mcc,
                    Status = m.Status.ToString()
                }).ToList()
            });
        }
    }
}

public static class GetAllMerchantsQueryEndpoint
{
    public static RouteGroupBuilder GetAllMerchantsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/",
                async (IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<GetAllMerchants.GetAllMerchantsResponse>>(
                            new GetAllMerchants.GetAllMerchantsQuery());
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("GetAllMerchants")
            .MapToApiVersion(1, 0)
            .Produces<GetAllMerchants.GetAllMerchantsResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}