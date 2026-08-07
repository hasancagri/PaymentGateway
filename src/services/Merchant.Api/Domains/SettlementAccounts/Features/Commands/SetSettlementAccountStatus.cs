namespace Merchant.Api.Domains.SettlementAccounts.Features.Commands;

public static class SetSettlementAccountStatus
{
    public record SetSettlementAccountStatusCommand(Guid MerchantId, Guid AccountId, bool IsActive);

    public class SetSettlementAccountStatusResponse
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    [Transactional]
    public class SetSettlementAccountStatusCommandHandler
    {
        public async Task<FeatureObjectResultModel<SetSettlementAccountStatusResponse>> Handle(
            SetSettlementAccountStatusCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            // Tenant filtreli yükleme; yoksa NotFound.
            var account = await session.Query<SettlementAccount>()
                .Where(a => a.Id == cmd.AccountId && a.MerchantId == cmd.MerchantId && !a.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (account is null)
                return FeatureObjectResultModel<SetSettlementAccountStatusResponse>.NotFound();

            if (cmd.IsActive)
                account.Activate();
            else
                account.Deactivate();

            session.Store(account);

            return FeatureObjectResultModel<SetSettlementAccountStatusResponse>.Ok(new SetSettlementAccountStatusResponse
            {
                Id = account.Id,
                Status = account.Status.ToString()
            });
        }
    }
}

public static class SetSettlementAccountStatusEndpoint
{
    public static RouteGroupBuilder SetSettlementAccountStatusGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/{accountId:guid}/status",
                async (Guid merchantId, Guid accountId, [FromBody] SetSettlementAccountStatusBody body, IMessageBus bus) =>
                {
                    var cmd = new SetSettlementAccountStatus.SetSettlementAccountStatusCommand(
                        merchantId, accountId, body.IsActive);
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<SetSettlementAccountStatus.SetSettlementAccountStatusResponse>>(cmd);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
                })
            .WithName("SetSettlementAccountStatus")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantWrite)
            .Produces<SetSettlementAccountStatus.SetSettlementAccountStatusResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }

    /// <summary>Gövde modeli; <c>merchantId</c>/<c>accountId</c> rotadan gelir (contracts §5, PUT).</summary>
    public record SetSettlementAccountStatusBody(bool IsActive);
}