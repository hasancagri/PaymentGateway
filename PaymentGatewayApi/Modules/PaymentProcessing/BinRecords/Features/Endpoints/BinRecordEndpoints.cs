using PaymentGatewayApi.Modules.PaymentProcessing.BinRecords.Features.Commands;
using PaymentGatewayApi.Modules.PaymentProcessing.BinRecords.Features.Queries;

namespace PaymentGatewayApi.Modules.PaymentProcessing.BinRecords.Features.Endpoints;

public static class BinRecordEndpoints
{
    public static IEndpointRouteBuilder MapBinRecordEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/bin-records");

        group.MapPost("/", async ([FromBody] CreateBinRecord.CreateBinRecordCommand cmd, IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateBinRecord.CreateBinRecordResponse>>(cmd);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<GetBinRecordById.GetBinRecordByIdResponse>>(
                    new GetBinRecordById.GetBinRecordByIdQuery { BinRecordId = id });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        return app;
    }
}