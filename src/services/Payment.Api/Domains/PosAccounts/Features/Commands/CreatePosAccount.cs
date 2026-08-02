namespace Payment.Api.Domains.PosAccounts.Features.Commands;

public static class CreatePosAccount
{
    public record CreatePosAccountCommand(
        string BankCode,
        string Title,
        string MerchantId,
        string MerchantUser,
        string MerchantPassword,
        string? MerchantStorekey,
        bool TestPlatform,
        List<CommissionRateDto> CommissionRates);

    public record CommissionRateDto(int InstallmentCount, decimal RatePercent);

    public class CreatePosAccountResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreatePosAccountCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreatePosAccountResponse>> Handle(
            CreatePosAccountCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var duplicate = await session.Query<PosAccount>()
                .Where(a => a.BankCode == cmd.BankCode && !a.IsDeleted)
                .AnyAsync(ct);
            if (duplicate)
            {
                return FeatureObjectResultModel<CreatePosAccountResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.BankCode),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_DUPLICATE
                });
            }

            var result = PosAccount.Create(cmd.BankCode, cmd.Title, cmd.MerchantId, cmd.MerchantUser,
                cmd.MerchantPassword, cmd.MerchantStorekey, cmd.TestPlatform);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<CreatePosAccountResponse>.Error(result.Messages);

            foreach (var rate in cmd.CommissionRates)
            {
                var rateResult = result.Data!.SetCommissionRate(rate.InstallmentCount, rate.RatePercent);
                if (!rateResult.IsSuccess)
                    return FeatureObjectResultModel<CreatePosAccountResponse>.Error(rateResult.Messages);
            }

            session.Store(result.Data!);

            return FeatureObjectResultModel<CreatePosAccountResponse>.Ok(new CreatePosAccountResponse
            {
                Id = result.Data!.Id
            });
        }
    }
}

public static class CreatePosAccountCommandEndpoint
{
    public static RouteGroupBuilder CreatePosAccountGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/",
                async ([FromBody] CreatePosAccount.CreatePosAccountCommand cmd, IMessageBus bus) =>
                {
                    var result =
                        await bus.InvokeAsync<FeatureObjectResultModel<CreatePosAccount.CreatePosAccountResponse>>(cmd);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("CreatePosAccount")
            .MapToApiVersion(1, 0)
            .Produces<CreatePosAccount.CreatePosAccountResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}