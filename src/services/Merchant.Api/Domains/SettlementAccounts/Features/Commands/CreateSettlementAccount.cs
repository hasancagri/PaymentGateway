using Merchant.Api.ReadModels;
using MerchantAggregate = Merchant.Api.Domains.Merchants.Merchant;

namespace Merchant.Api.Domains.SettlementAccounts.Features.Commands;

public static class CreateSettlementAccount
{
    public record CreateSettlementAccountCommand(
        Guid MerchantId,
        string BankCode,
        string Iban,
        string AccountOwnerName,
        string AccountNo,
        string AccountDescription);

    public class CreateSettlementAccountResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateSettlementAccountCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateSettlementAccountResponse>> Handle(
            CreateSettlementAccountCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            // 1) Saf format doğrulaması aggregate'te (IBAN normalize + mod-97 dahil).
            var result = SettlementAccount.Create(cmd.MerchantId, cmd.BankCode, cmd.Iban,
                cmd.AccountOwnerName, cmd.AccountNo, cmd.AccountDescription);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<CreateSettlementAccountResponse>.Error(result.Messages);

            // 2) Varlık + benzersizlik doğrulaması handler'da (referans veri + Marten sorgusu).
            var errors = new List<MessageItem>();

            var merchantExists = await session.Query<MerchantAggregate>()
                .AnyAsync(m => m.Id == cmd.MerchantId && !m.IsDeleted, ct);
            if (!merchantExists)
                errors.Add(NotFound(nameof(cmd.MerchantId)));

            var bank = await session.LoadAsync<ReferenceBank>(cmd.BankCode?.Trim() ?? string.Empty, ct);
            if (bank is null)
                errors.Add(NotFound(nameof(cmd.BankCode)));

            var normalizedIban = result.Data!.Iban;
            var duplicate = await session.Query<SettlementAccount>()
                .AnyAsync(a => a.MerchantId == cmd.MerchantId && a.Iban == normalizedIban && !a.IsDeleted, ct);
            if (duplicate)
                errors.Add(new MessageItem
                {
                    Property = nameof(cmd.Iban),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_DUPLICATE
                });

            if (errors.Count > 0)
                return FeatureObjectResultModel<CreateSettlementAccountResponse>.Error(errors);

            session.Store(result.Data!);

            // 013 US5 — Active koşulu #1 (BC-içi kanca). Settlement eklenince merchant'ı işaretle +
            // TryActivate (idempotent). 3 koşul dolduysa Provisioning→Active + event (aynı [Transactional]).
            var merchant = await session.LoadAsync<MerchantAggregate>(cmd.MerchantId, ct);
            if (merchant is not null)
            {
                merchant.MarkSettlementAccountPresent();
                var activated = merchant.TryActivate();
                session.Update(merchant);

                if (activated)
                    await bus.PublishAsync(new Shared.IntegrationEvents.MerchantStatusChanged(
                        merchant.Id, Merchant.Api.Domains.Merchants.MerchantStatus.Active.ToString()));
            }

            return FeatureObjectResultModel<CreateSettlementAccountResponse>.Ok(new CreateSettlementAccountResponse
            {
                Id = result.Data!.Id
            });
        }

        private static MessageItem NotFound(string property) => new()
        {
            Property = property,
            Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
        };
    }
}

public static class CreateSettlementAccountEndpoint
{
    public static RouteGroupBuilder CreateSettlementAccountGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/",
                async (Guid merchantId, [FromBody] CreateSettlementAccountBody body, IMessageBus bus) =>
                {
                    var cmd = new CreateSettlementAccount.CreateSettlementAccountCommand(
                        merchantId, body.BankCode, body.Iban, body.AccountOwnerName,
                        body.AccountNo, body.AccountDescription);
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<CreateSettlementAccount.CreateSettlementAccountResponse>>(cmd);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("CreateSettlementAccount")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantWrite, AuthorizationPolicies.MerchantScoped)
            .Produces<CreateSettlementAccount.CreateSettlementAccountResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }

    /// <summary>Gövde modeli; <c>merchantId</c> rotadan gelir (contracts §1).</summary>
    public record CreateSettlementAccountBody(
        string BankCode,
        string Iban,
        string AccountOwnerName,
        string AccountNo,
        string AccountDescription);
}