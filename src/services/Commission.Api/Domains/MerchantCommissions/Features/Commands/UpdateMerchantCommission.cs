using Commission.Api.Domains.BankCommissions;

namespace Commission.Api.Domains.MerchantCommissions.Features.Commands;

public static class UpdateMerchantCommission
{
    public record UpdateMerchantCommissionCommand(Guid Id, decimal Rate);

    public class UpdateMerchantCommissionResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class UpdateMerchantCommissionCommandHandler
    {
        public async Task<FeatureObjectResultModel<UpdateMerchantCommissionResponse>> Handle(
            UpdateMerchantCommissionCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var merchantCommission = await session.LoadAsync<MerchantCommission>(cmd.Id, ct);
            if (merchantCommission is null || merchantCommission.IsDeleted)
                return FeatureObjectResultModel<UpdateMerchantCommissionResponse>.NotFound();

            // Bağlı banka oranı invariant referansı (aynı session, in-process).
            var bankCommission = await session.LoadAsync<BankCommission>(merchantCommission.BankCommissionId, ct);
            if (bankCommission is null || bankCommission.IsDeleted)
            {
                return FeatureObjectResultModel<UpdateMerchantCommissionResponse>.Error(new MessageItem
                {
                    Property = nameof(merchantCommission.BankCommissionId),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });
            }

            var result = merchantCommission.UpdateRate(cmd.Rate, bankCommission);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<UpdateMerchantCommissionResponse>.Error(result.Messages);

            session.Store(merchantCommission);

            return FeatureObjectResultModel<UpdateMerchantCommissionResponse>.Ok(new UpdateMerchantCommissionResponse
            {
                Id = merchantCommission.Id
            });
        }
    }
}

public static class UpdateMerchantCommissionCommandEndpoint
{
    public static RouteGroupBuilder UpdateMerchantCommissionGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}",
                async (Guid id, [FromBody] UpdateMerchantCommissionRequest body, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<UpdateMerchantCommission.UpdateMerchantCommissionResponse>>(
                            new UpdateMerchantCommission.UpdateMerchantCommissionCommand(id, body.Rate));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("UpdateMerchantCommission")
            .MapToApiVersion(1, 0)
            .Produces<UpdateMerchantCommission.UpdateMerchantCommissionResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }

    public record UpdateMerchantCommissionRequest(decimal Rate);
}