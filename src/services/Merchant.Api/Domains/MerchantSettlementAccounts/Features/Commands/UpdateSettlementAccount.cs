using Merchant.Api.Domains.MerchantSettlementAccounts.Lookups;

namespace Merchant.Api.Domains.MerchantSettlementAccounts.Features.Commands;

public static class UpdateSettlementAccount
{
    public record UpdateSettlementAccountCommand(
        Guid MerchantId,
        Guid AccountId,
        string BankCode,
        string Iban,
        string AccountOwnerName,
        string AccountNo,
        string AccountDescription);

    public class UpdateSettlementAccountResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class UpdateSettlementAccountCommandHandler
    {
        public async Task<FeatureObjectResultModel<UpdateSettlementAccountResponse>> Handle(
            UpdateSettlementAccountCommand cmd,
            IDocumentSession session,
            IBankCodeLookup bankLookup,
            CancellationToken ct)
        {
            // Tenant filtreli yükleme; yoksa NotFound.
            var account = await session.Query<MerchantSettlementAccount>()
                .Where(a => a.Id == cmd.AccountId && a.MerchantId == cmd.MerchantId && !a.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (account is null)
                return FeatureObjectResultModel<UpdateSettlementAccountResponse>.NotFound();

            // Varlık + benzersizlik (kendisi hariç).
            var errors = new List<MessageItem>();
            if (!bankLookup.Exists(cmd.BankCode))
                errors.Add(new MessageItem
                {
                    Property = nameof(cmd.BankCode),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            // Saf doğrulama + alan güncelleme (bozuksa eski değerler korunur — Store çağrılmaz).
            var result = account.UpdateDetails(cmd.BankCode, cmd.Iban, cmd.AccountOwnerName,
                cmd.AccountNo, cmd.AccountDescription);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<UpdateSettlementAccountResponse>.Error(result.Messages);

            // Normalize IBAN ile mükerrer kontrolü (kendisi hariç).
            var normalizedIban = account.Iban;
            var duplicate = await session.Query<MerchantSettlementAccount>()
                .AnyAsync(a => a.MerchantId == cmd.MerchantId && a.Iban == normalizedIban &&
                               a.Id != cmd.AccountId && !a.IsDeleted, ct);
            if (duplicate)
                errors.Add(new MessageItem
                {
                    Property = nameof(cmd.Iban),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_DUPLICATE
                });

            if (errors.Count > 0)
                return FeatureObjectResultModel<UpdateSettlementAccountResponse>.Error(errors);

            session.Store(account);

            return FeatureObjectResultModel<UpdateSettlementAccountResponse>.Ok(new UpdateSettlementAccountResponse
            {
                Id = account.Id
            });
        }
    }
}

public static class UpdateSettlementAccountEndpoint
{
    public static RouteGroupBuilder UpdateSettlementAccountGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/{accountId:guid}",
                async (Guid merchantId, Guid accountId, [FromBody] UpdateSettlementAccountBody body, IMessageBus bus) =>
                {
                    var cmd = new UpdateSettlementAccount.UpdateSettlementAccountCommand(
                        merchantId, accountId, body.BankCode, body.Iban, body.AccountOwnerName,
                        body.AccountNo, body.AccountDescription);
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<UpdateSettlementAccount.UpdateSettlementAccountResponse>>(cmd);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("UpdateSettlementAccount")
            .MapToApiVersion(1, 0)
            .Produces<UpdateSettlementAccount.UpdateSettlementAccountResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }

    /// <summary>Gövde modeli; <c>merchantId</c>/<c>accountId</c> rotadan gelir (contracts §4).</summary>
    public record UpdateSettlementAccountBody(
        string BankCode,
        string Iban,
        string AccountOwnerName,
        string AccountNo,
        string AccountDescription);
}