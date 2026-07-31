using Commission.Api.Domains.BankCommissions;

namespace Commission.Api.Domains.MerchantCommissions.Features.Commands;

public static class CreateMerchantCommission
{
    public record CreateMerchantCommissionCommand(
        Guid MerchantId,
        Guid BankCommissionId,
        decimal Rate);

    public class CreateMerchantCommissionResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateMerchantCommissionCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateMerchantCommissionResponse>> Handle(
            CreateMerchantCommissionCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            // Bağlı banka oranını yükle (invariant referansı). merchantId'ye Merchant.Api'ye çağrı YOK.
            var bankCommission = await session.LoadAsync<BankCommission>(cmd.BankCommissionId, ct);
            if (bankCommission is null || bankCommission.IsDeleted)
            {
                return FeatureObjectResultModel<CreateMerchantCommissionResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.BankCommissionId),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });
            }

            // (MerchantId, BankCommissionId) zaten varsa → upsert (UpdateRate); yoksa Create.
            var existing = await session.Query<MerchantCommission>()
                .Where(c => c.MerchantId == cmd.MerchantId
                            && c.BankCommissionId == cmd.BankCommissionId
                            && !c.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (existing is not null)
            {
                var updateResult = existing.UpdateRate(cmd.Rate, bankCommission);
                if (!updateResult.IsSuccess)
                    return FeatureObjectResultModel<CreateMerchantCommissionResponse>.Error(updateResult.Messages);

                session.Store(existing);
                return FeatureObjectResultModel<CreateMerchantCommissionResponse>.Ok(new CreateMerchantCommissionResponse
                {
                    Id = existing.Id
                });
            }

            var result = MerchantCommission.Create(cmd.MerchantId, bankCommission, cmd.Rate);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<CreateMerchantCommissionResponse>.Error(result.Messages);

            session.Store(result.Data!);

            return FeatureObjectResultModel<CreateMerchantCommissionResponse>.Ok(new CreateMerchantCommissionResponse
            {
                Id = result.Data!.Id
            });
        }
    }
}

public static class CreateMerchantCommissionCommandEndpoint
{
    public static RouteGroupBuilder CreateMerchantCommissionGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/",
                async ([FromBody] CreateMerchantCommission.CreateMerchantCommissionCommand cmd, IMessageBus bus) =>
                {
                    var result =
                        await bus.InvokeAsync<FeatureObjectResultModel<CreateMerchantCommission.CreateMerchantCommissionResponse>>(cmd);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("CreateMerchantCommission")
            .MapToApiVersion(1, 0)
            .Produces<CreateMerchantCommission.CreateMerchantCommissionResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}