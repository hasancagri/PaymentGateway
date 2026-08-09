
namespace Merchant.Api.Domains.SettlementAccounts.Features.Queries;

public static class GetSettlementAccounts
{
    public record GetSettlementAccountsQuery(Guid MerchantId);

    public class GetSettlementAccountsResponse
    {
        public List<SettlementAccountItem> Accounts { get; set; } = new();
    }

    public class SettlementAccountItem
    {
        public Guid Id { get; set; }
        public string BankCode { get; set; } = string.Empty;
        public string? BankName { get; set; }
        public string Iban { get; set; } = string.Empty;
        public string AccountOwnerName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class GetSettlementAccountsQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetSettlementAccountsResponse>> Handle(
            GetSettlementAccountsQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            // Tenant sınırı: her zaman MerchantId ile filtrele (SC-003).
            var accounts = await session.Query<SettlementAccount>()
                .Where(a => a.MerchantId == query.MerchantId && !a.IsDeleted)
                .ToListAsync(ct);

            // Banka adları: tek toplu yükleme (id = Code), sonra sözlükten eşle.
            var bankCodes = accounts.Select(a => a.BankCode).Distinct().ToArray();
            var bankNames = (await session.LoadManyAsync<ReferenceBank>(ct, bankCodes))
                .ToDictionary(b => b.Code, b => b.Name);

            return FeatureObjectResultModel<GetSettlementAccountsResponse>.Ok(
                new GetSettlementAccountsResponse
                {
                    Accounts = accounts.Select(a => new SettlementAccountItem
                    {
                        Id = a.Id,
                        BankCode = a.BankCode,
                        BankName = bankNames.GetValueOrDefault(a.BankCode),
                        Iban = a.Iban,
                        AccountOwnerName = a.AccountOwnerName,
                        Status = a.Status.ToString()
                    }).ToList()
                });
        }
    }
}

public static class GetSettlementAccountsQueryEndpoint
{
    public static RouteGroupBuilder GetSettlementAccountsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/",
                async (Guid merchantId, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<GetSettlementAccounts.GetSettlementAccountsResponse>>(
                            new GetSettlementAccounts.GetSettlementAccountsQuery(merchantId));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("GetSettlementAccounts")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantRead, AuthorizationPolicies.MerchantScoped)
            .Produces<GetSettlementAccounts.GetSettlementAccountsResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}