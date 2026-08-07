namespace Payment.Api.Domains.PosAccounts.Features.Commands;

public static class UpdatePosAccount
{
    public record UpdatePosAccountCommand(
        Guid Id,
        string MerchantId,
        string MerchantUser,
        string MerchantPassword,
        string? MerchantStorekey,
        bool TestPlatform,
        bool IsActive,
        List<CreatePosAccount.CommissionRateDto> CommissionRates);

    public class UpdatePosAccountResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class UpdatePosAccountCommandHandler
    {
        public async Task<FeatureObjectResultModel<UpdatePosAccountResponse>> Handle(
            UpdatePosAccountCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var account = await session.LoadAsync<PosAccount>(cmd.Id, ct);
            if (account is null || account.IsDeleted)
                return FeatureObjectResultModel<UpdatePosAccountResponse>.NotFound();

            var credentialResult = account.UpdateCredentials(cmd.MerchantId, cmd.MerchantUser,
                cmd.MerchantPassword, cmd.MerchantStorekey, cmd.TestPlatform);
            if (!credentialResult.IsSuccess)
                return FeatureObjectResultModel<UpdatePosAccountResponse>.Error(credentialResult.Messages);

            // Komisyon tablosu komple değiştirilir: önce mevcutlar temizlenir, gelen liste kurulur.
            foreach (var existing in account.CommissionRates.ToList())
                account.RemoveCommissionRate(existing.InstallmentCount);

            foreach (var rate in cmd.CommissionRates)
            {
                var rateResult = account.SetCommissionRate(rate.InstallmentCount, rate.RatePercent);
                if (!rateResult.IsSuccess)
                    return FeatureObjectResultModel<UpdatePosAccountResponse>.Error(rateResult.Messages);
            }

            if (cmd.IsActive)
                account.Activate();
            else
                account.Deactivate();

            session.Store(account);

            return FeatureObjectResultModel<UpdatePosAccountResponse>.Ok(new UpdatePosAccountResponse
            {
                Id = account.Id
            });
        }
    }
}

public static class UpdatePosAccountCommandEndpoint
{
    public static RouteGroupBuilder UpdatePosAccountGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}",
                async (Guid id, [FromBody] UpdatePosAccount.UpdatePosAccountCommand cmd, IMessageBus bus) =>
                {
                    var result =
                        await bus.InvokeAsync<FeatureObjectResultModel<UpdatePosAccount.UpdatePosAccountResponse>>(
                            cmd with { Id = id });
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("UpdatePosAccount")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.PaymentWrite)
            .Produces<UpdatePosAccount.UpdatePosAccountResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}