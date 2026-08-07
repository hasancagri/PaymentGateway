using Merchant.Api.ReadModels;

namespace Merchant.Api.Domains.SettlementAccounts.Features.Queries;

public static class GetSettlementAccount
{
    public record GetSettlementAccountQuery(Guid MerchantId, Guid AccountId);

    public class GetSettlementAccountResponse
    {
        public Guid Id { get; set; }
        public Guid MerchantId { get; set; }
        public string BankCode { get; set; } = string.Empty;
        public string? BankName { get; set; }
        public string Iban { get; set; } = string.Empty;
        public string AccountOwnerName { get; set; } = string.Empty;
        public string AccountNo { get; set; } = string.Empty;
        public string AccountDescription { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; }
    }

    public class GetSettlementAccountQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetSettlementAccountResponse>> Handle(
            GetSettlementAccountQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            // Tenant filtreli yükleme: başka merchant'ın accountId'si NotFound (sızıntı yok).
            var account = await session.Query<SettlementAccount>()
                .Where(a => a.Id == query.AccountId && a.MerchantId == query.MerchantId && !a.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (account is null)
                return FeatureObjectResultModel<GetSettlementAccountResponse>.NotFound();

            var bank = await session.LoadAsync<ReferenceBank>(account.BankCode, ct);

            return FeatureObjectResultModel<GetSettlementAccountResponse>.Ok(new GetSettlementAccountResponse
            {
                Id = account.Id,
                MerchantId = account.MerchantId,
                BankCode = account.BankCode,
                BankName = bank?.Name,
                Iban = account.Iban,
                AccountOwnerName = account.AccountOwnerName,
                AccountNo = account.AccountNo,
                AccountDescription = account.AccountDescription,
                Status = account.Status.ToString(),
                CreatedTime = account.CreatedTime
            });
        }
    }
}

public static class GetSettlementAccountQueryEndpoint
{
    public static RouteGroupBuilder GetSettlementAccountGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{accountId:guid}",
                async (Guid merchantId, Guid accountId, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<GetSettlementAccount.GetSettlementAccountResponse>>(
                            new GetSettlementAccount.GetSettlementAccountQuery(merchantId, accountId));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
                })
            .WithName("GetSettlementAccount")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantRead)
            .Produces<GetSettlementAccount.GetSettlementAccountResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}