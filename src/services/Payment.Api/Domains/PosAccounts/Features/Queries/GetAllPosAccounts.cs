namespace Payment.Api.Domains.PosAccounts.Features.Queries;

public static class GetAllPosAccounts
{
    public record GetAllPosAccountsQuery;

    public class GetAllPosAccountsResponse
    {
        public List<PosAccountItem> Accounts { get; set; } = new();
    }

    /// <summary>Liste görünümü; kimlik bilgileri (şifre/anahtar) bilinçli olarak dışarı verilmez.</summary>
    public class PosAccountItem
    {
        public Guid Id { get; set; }
        public string BankCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string MerchantId { get; set; } = string.Empty;
        public bool TestPlatform { get; set; }
        public bool IsActive { get; set; }
        public List<CommissionRateItem> CommissionRates { get; set; } = new();
    }

    public class CommissionRateItem
    {
        public int InstallmentCount { get; set; }
        public decimal RatePercent { get; set; }
    }

    public class GetAllPosAccountsQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetAllPosAccountsResponse>> Handle(
            GetAllPosAccountsQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var accounts = await session.Query<PosAccount>()
                .Where(a => !a.IsDeleted)
                .ToListAsync(ct);

            return FeatureObjectResultModel<GetAllPosAccountsResponse>.Ok(new GetAllPosAccountsResponse
            {
                Accounts = accounts.Select(a => new PosAccountItem
                {
                    Id = a.Id,
                    BankCode = a.BankCode,
                    Title = a.Title,
                    MerchantId = a.MerchantId,
                    TestPlatform = a.TestPlatform,
                    IsActive = a.IsActive,
                    CommissionRates = a.CommissionRates
                        .OrderBy(r => r.InstallmentCount)
                        .Select(r => new CommissionRateItem
                        {
                            InstallmentCount = r.InstallmentCount,
                            RatePercent = r.RatePercent
                        }).ToList()
                }).ToList()
            });
        }
    }
}

public static class GetAllPosAccountsQueryEndpoint
{
    public static RouteGroupBuilder GetAllPosAccountsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/",
                async (IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<GetAllPosAccounts.GetAllPosAccountsResponse>>(
                            new GetAllPosAccounts.GetAllPosAccountsQuery());
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("GetAllPosAccounts")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.PaymentRead)
            .Produces<GetAllPosAccounts.GetAllPosAccountsResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}