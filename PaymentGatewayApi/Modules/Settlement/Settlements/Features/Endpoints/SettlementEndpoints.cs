using PaymentGatewayApi.Modules.Settlement.Settlements.Features.Commands;

namespace PaymentGatewayApi.Modules.Settlement.Settlements.Features.Endpoints;

public static class SettlementEndpoints
{
    public static IEndpointRouteBuilder MapSettlementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/settlements");

        group.MapPost("/{id:guid}/lines",
            async ([FromBody] AddSettlementLine.AddSettlementLineCommand cmd, IMessageBus bus) =>
            {
                var result =
                    await bus.InvokeAsync<FeatureObjectResultModel<AddSettlementLine.AddSettlementLineCommandResponse>>(
                        cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            });

        group.MapPost("/{id:guid}/mark-processing", async (Guid id, IMessageBus bus) =>
        {
            var result =
                await bus
                    .InvokeAsync<FeatureObjectResultModel<
                        MarkSettlementProcessing.MarkSettlementProcessingCommandResponse>>(
                        new MarkSettlementProcessing.MarkSettlementProcessingCommand { SettlementId = id });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapPost("/{id:guid}/complete", async (Guid id, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<CompleteSettlement.CompleteSettlementCommandResponse>>(
                    new CompleteSettlement.CompleteSettlementCommand { SettlementId = id });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapPost("/{id:guid}/fail", async ([FromBody] FailSettlement.FailSettlementCommand cmd, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<FailSettlement.FailSettlementCommandResponse>>(cmd);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        return app;
    }
}