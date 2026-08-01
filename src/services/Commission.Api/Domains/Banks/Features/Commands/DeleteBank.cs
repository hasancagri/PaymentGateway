using Commission.Api.Domains.Banks;
using Commission.Api.Domains.BankCommissions;

namespace Commission.Api.Domains.Banks.Features.Commands;

public static class DeleteBank
{
    public record DeleteBankCommand(string Code);

    public class DeleteBankResponse
    {
        public string Code { get; set; } = string.Empty;
    }

    [Transactional]
    public class DeleteBankCommandHandler
    {
        public async Task<FeatureObjectResultModel<DeleteBankResponse>> Handle(
            DeleteBankCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var bank = await session.Query<Bank>()
                .Where(b => b.Code == cmd.Code && !b.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (bank is null)
            {
                return FeatureObjectResultModel<DeleteBankResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Code),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });
            }

            // Guard: bankaya bağlı (aynı BankCode, silinmemiş) komisyon varsa silme engellenir.
            var hasCommissions = await session.Query<BankCommission>()
                .Where(c => c.BankCode == cmd.Code && !c.IsDeleted)
                .AnyAsync(ct);

            if (hasCommissions)
            {
                return FeatureObjectResultModel<DeleteBankResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Code),
                    Code = CommissionResourceConstants.BANK_HAS_COMMISSIONS
                });
            }

            bank.SoftDelete();
            session.Update(bank);

            return FeatureObjectResultModel<DeleteBankResponse>.Ok(new DeleteBankResponse { Code = bank.Code });
        }
    }
}

public static class DeleteBankCommandEndpoint
{
    public static RouteGroupBuilder DeleteBankGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapDelete("/{code}",
                async (string code, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<DeleteBank.DeleteBankResponse>>(
                        new DeleteBank.DeleteBankCommand(code));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("DeleteBank")
            .MapToApiVersion(1, 0)
            .Produces<DeleteBank.DeleteBankResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}