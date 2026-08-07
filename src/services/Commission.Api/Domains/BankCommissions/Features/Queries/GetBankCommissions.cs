namespace Commission.Api.Domains.BankCommissions.Features.Queries;

public static class GetBankCommissions
{
    public record GetBankCommissionsQuery(string? BankCode);

    public class GetBankCommissionsResponse
    {
        public List<BankCommissionItem> Items { get; set; } = new();
    }

    public class BankCommissionItem
    {
        public Guid Id { get; set; }
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

    public class GetBankCommissionsQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetBankCommissionsResponse>> Handle(
            GetBankCommissionsQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var q = session.Query<BankCommission>().Where(b => !b.IsDeleted);

            if (!string.IsNullOrWhiteSpace(query.BankCode))
                q = q.Where(b => b.BankCode == query.BankCode);

            var items = await q.ToListAsync(ct);

            return FeatureObjectResultModel<GetBankCommissionsResponse>.Ok(new GetBankCommissionsResponse
            {
                Items = items.Select(b => new BankCommissionItem
                {
                    Id = b.Id,
                    BankCode = b.BankCode,
                    Rate = b.Rate,
                    Criteria = new CriteriaView
                    {
                        CardBrand = b.Criteria.CardBrand.ToString(),
                        CardType = b.Criteria.CardType.ToString(),
                        TransactionRegion = b.Criteria.TransactionRegion.ToString(),
                        InstallmentCount = b.Criteria.InstallmentCount
                    }
                }).ToList()
            });
        }
    }
}

public static class GetBankCommissionsQueryEndpoint
{
    public static RouteGroupBuilder GetBankCommissionsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/",
                async ([FromQuery] string? bankCode, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<GetBankCommissions.GetBankCommissionsResponse>>(
                            new GetBankCommissions.GetBankCommissionsQuery(bankCode));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("GetBankCommissions")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.CommissionRead)
            .Produces<GetBankCommissions.GetBankCommissionsResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}