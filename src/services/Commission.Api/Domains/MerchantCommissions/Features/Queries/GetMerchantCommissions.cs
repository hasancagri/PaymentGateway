namespace Commission.Api.Domains.MerchantCommissions.Features.Queries;

public static class GetMerchantCommissions
{
    public record GetMerchantCommissionsQuery(Guid MerchantId);

    public class GetMerchantCommissionsResponse
    {
        public List<MerchantCommissionItem> Items { get; set; } = new();
    }

    public class MerchantCommissionItem
    {
        public Guid Id { get; set; }
        public Guid MerchantId { get; set; }
        public Guid BankCommissionId { get; set; }
        public string BankCode { get; set; } = string.Empty;
        public CriteriaView Criteria { get; set; } = new();
        public decimal Rate { get; set; }
    }

    public class CriteriaView
    {
        public string CardBrand { get; set; } = string.Empty;
        public string CardType { get; set; } = string.Empty;
        public string TransactionRegion { get; set; } = string.Empty;
        public int InstallmentCount { get; set; }
    }

    public class GetMerchantCommissionsQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetMerchantCommissionsResponse>> Handle(
            GetMerchantCommissionsQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            // Düz MerchantId filtresi (Marten conjoined tenant ertelendi). Başka merchant sızmaz (SC-004).
            var items = await session.Query<MerchantCommission>()
                .Where(c => c.MerchantId == query.MerchantId && !c.IsDeleted)
                .ToListAsync(ct);

            return FeatureObjectResultModel<GetMerchantCommissionsResponse>.Ok(new GetMerchantCommissionsResponse
            {
                Items = items.Select(c => new MerchantCommissionItem
                {
                    Id = c.Id,
                    MerchantId = c.MerchantId,
                    BankCommissionId = c.BankCommissionId,
                    BankCode = c.BankCode,
                    Rate = c.Rate,
                    Criteria = new CriteriaView
                    {
                        CardBrand = c.Criteria.CardBrand.ToString(),
                        CardType = c.Criteria.CardType.ToString(),
                        TransactionRegion = c.Criteria.TransactionRegion.ToString(),
                        InstallmentCount = c.Criteria.InstallmentCount
                    }
                }).ToList()
            });
        }
    }
}

public static class GetMerchantCommissionsQueryEndpoint
{
    public static RouteGroupBuilder GetMerchantCommissionsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/",
                async ([FromQuery] Guid merchantId, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<GetMerchantCommissions.GetMerchantCommissionsResponse>>(
                            new GetMerchantCommissions.GetMerchantCommissionsQuery(merchantId));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("GetMerchantCommissions")
            .MapToApiVersion(1, 0)
            .Produces<GetMerchantCommissions.GetMerchantCommissionsResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}