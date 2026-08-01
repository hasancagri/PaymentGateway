using Merchant.Api.Domains.MerchantSettlementAccounts.Lookups;

namespace Merchant.Api.Domains.MerchantSettlementAccounts.Features.Queries;

public static class GetMerchantSettlementAccounts
{
    public record GetMerchantSettlementAccountsQuery(Guid MerchantId);

    public class GetMerchantSettlementAccountsResponse
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

    public class GetMerchantSettlementAccountsQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetMerchantSettlementAccountsResponse>> Handle(
            GetMerchantSettlementAccountsQuery query,
            IDocumentSession session,
            IBankCodeLookup bankLookup,
            CancellationToken ct)
        {
            // Tenant sınırı: her zaman MerchantId ile filtrele (SC-003).
            var accounts = await session.Query<MerchantSettlementAccount>()
                .Where(a => a.MerchantId == query.MerchantId && !a.IsDeleted)
                .ToListAsync(ct);

            return FeatureObjectResultModel<GetMerchantSettlementAccountsResponse>.Ok(
                new GetMerchantSettlementAccountsResponse
                {
                    Accounts = accounts.Select(a => new SettlementAccountItem
                    {
                        Id = a.Id,
                        BankCode = a.BankCode,
                        BankName = bankLookup.NameOf(a.BankCode),
                        Iban = a.Iban,
                        AccountOwnerName = a.AccountOwnerName,
                        Status = a.Status.ToString()
                    }).ToList()
                });
        }
    }
}

public static class GetMerchantSettlementAccountsQueryEndpoint
{
    public static RouteGroupBuilder GetMerchantSettlementAccountsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/",
                async (Guid merchantId, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<GetMerchantSettlementAccounts.GetMerchantSettlementAccountsResponse>>(
                            new GetMerchantSettlementAccounts.GetMerchantSettlementAccountsQuery(merchantId));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("GetMerchantSettlementAccounts")
            .MapToApiVersion(1, 0)
            .Produces<GetMerchantSettlementAccounts.GetMerchantSettlementAccountsResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}