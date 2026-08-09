
namespace Commission.Api.Domains.Banks.Features.Commands;

public static class UpdateBank
{
    // Ad ve kod katalogdan; komut yalnız aktiflik + taksit taşır.
    public record UpdateBankCommand(
        string Code,
        bool IsActive,
        List<int> SupportedInstallments);

    public class UpdateBankResponse
    {
        public string Code { get; set; } = string.Empty;
    }

    [Transactional]
    public class UpdateBankCommandHandler
    {
        public async Task<FeatureObjectResultModel<UpdateBankResponse>> Handle(
            UpdateBankCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var bank = await session.Query<Bank>()
                .Where(b => b.Code == cmd.Code && !b.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (bank is null)
            {
                return FeatureObjectResultModel<UpdateBankResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Code),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });
            }

            var result = bank.Update(cmd.IsActive, cmd.SupportedInstallments ?? new List<int>());
            if (!result.IsSuccess)
                return FeatureObjectResultModel<UpdateBankResponse>.Error(result.Messages);

            session.Update(bank);

            return FeatureObjectResultModel<UpdateBankResponse>.Ok(new UpdateBankResponse { Code = bank.Code });
        }
    }
}

public static class UpdateBankCommandEndpoint
{
    public static RouteGroupBuilder UpdateBankGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/{code}",
                async (string code, [FromBody] UpdateBankRequest body, IMessageBus bus) =>
                {
                    // Code rota parametresinden otoriter; Name katalogdan (gövdede yok → immutable).
                    var cmd = new UpdateBank.UpdateBankCommand(code, body.IsActive, body.SupportedInstallments);
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<UpdateBank.UpdateBankResponse>>(cmd);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("UpdateBank")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.CommissionWrite)
            .Produces<UpdateBank.UpdateBankResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }

    public record UpdateBankRequest(bool IsActive, List<int> SupportedInstallments);
}