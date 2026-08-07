namespace Commission.Api.Domains.MerchantCommissions.Features.Commands;

/// <summary>
/// Var olan merchant komisyonunun oranını günceller. Banka-bağımsız: banka YÜKLENMEZ.
/// Criteria/MerchantId değişmez; yalnız <c>Rate</c> (> 0).
/// </summary>
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

            var result = merchantCommission.UpdateRate(cmd.Rate);
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
            .RequireAuthorization(AuthorizationScopes.CommissionWrite)
            .Produces<UpdateMerchantCommission.UpdateMerchantCommissionResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }

    public record UpdateMerchantCommissionRequest(decimal Rate);
}