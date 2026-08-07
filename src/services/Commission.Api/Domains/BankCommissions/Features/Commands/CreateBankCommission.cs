namespace Commission.Api.Domains.BankCommissions.Features.Commands;

public static class CreateBankCommission
{
    public record CriteriaDto(
        string CardBrand,
        string CardType,
        string TransactionRegion,
        int InstallmentCount);

    public record CreateBankCommissionCommand(
        string BankCode,
        CriteriaDto Criteria,
        decimal Rate);

    public class CreateBankCommissionResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateBankCommissionCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateBankCommissionResponse>> Handle(
            CreateBankCommissionCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var criteriaResult = Criteria.FromCodes(cmd.Criteria.CardBrand, cmd.Criteria.CardType,
                cmd.Criteria.TransactionRegion, cmd.Criteria.InstallmentCount);
            if (!criteriaResult.IsSuccess)
                return FeatureObjectResultModel<CreateBankCommissionResponse>.Error(criteriaResult.Messages);

            var criteria = criteriaResult.Data!;

            // Benzersizlik (BankCode, Criteria): BankCode ile filtrele, Criteria'yı bellekte karşılaştır
            // (nested value-object LINQ'tan kaçın; BankCode kardinalitesi düşük).
            var sameBank = await session.Query<BankCommission>()
                .Where(b => b.BankCode == cmd.BankCode && !b.IsDeleted)
                .ToListAsync(ct);

            if (sameBank.Any(b => Equals(b.Criteria, criteria)))
            {
                return FeatureObjectResultModel<CreateBankCommissionResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.BankCode),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_DUPLICATE
                });
            }

            var result = BankCommission.Create(cmd.BankCode, criteria, cmd.Rate);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<CreateBankCommissionResponse>.Error(result.Messages);

            session.Store(result.Data!);

            return FeatureObjectResultModel<CreateBankCommissionResponse>.Ok(new CreateBankCommissionResponse
            {
                Id = result.Data!.Id
            });
        }
    }
}

public static class CreateBankCommissionCommandEndpoint
{
    public static RouteGroupBuilder CreateBankCommissionGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/",
                async ([FromBody] CreateBankCommission.CreateBankCommissionCommand cmd, IMessageBus bus) =>
                {
                    var result =
                        await bus.InvokeAsync<FeatureObjectResultModel<CreateBankCommission.CreateBankCommissionResponse>>(cmd);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("CreateBankCommission")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.CommissionWrite)
            .Produces<CreateBankCommission.CreateBankCommissionResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}