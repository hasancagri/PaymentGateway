using PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Features.Commands;
using PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Features.Queries;

namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Features.Endpoints;

public static class PaymentTransactionEndpoints
{
    private record FailRequest(string Reason);

    public static IEndpointRouteBuilder MapPaymentTransactionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/transactions");

        group.MapPost("/", async ([FromBody] InitiatePayment.InitiatePaymentCommand cmd, IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<InitiatePayment.InitiatePaymentResponse>>(cmd);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<GetTransactionById.GetTransactionByIdResponse>>(
                    new GetTransactionById.GetTransactionByIdQuery { TransactionId = id });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapGet("/", async ([FromQuery] Guid merchantId, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<List<GetTransactionsByMerchant.TransactionListItem>>>(
                    new GetTransactionsByMerchant.GetTransactionsByMerchantQuery { MerchantId = merchantId });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapPost("/{id:guid}/approve",
            async ([FromBody] ApproveTransaction.ApproveTransactionCommand cmd, IMessageBus bus) =>
            {
                var result =
                    await bus
                        .InvokeAsync<FeatureObjectResultModel<ApproveTransaction.ApproveTransactionCommandResponse>>(
                            cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            });

        group.MapPost("/{id:guid}/decline",
            async ([FromBody] DeclineTransaction.DeclineTransactionCommand cmd, IMessageBus bus) =>
            {
                var result =
                    await bus
                        .InvokeAsync<FeatureObjectResultModel<DeclineTransaction.DeclineTransactionCommandResponse>>(
                            cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            });

        group.MapPost("/{id:guid}/fail", async (Guid id, [FromBody] FailRequest req, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<FailTransaction.FailTransactionCommandResponse>>(
                    new FailTransaction.FailTransactionCommand { TransactionId = id, Reason = req.Reason });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapPost("/{id:guid}/assign-bank",
            async ([FromBody] AssignBankToTransaction.AssignBankToTransactionCommand cmd, IMessageBus bus) =>
            {
                var result =
                    await bus
                        .InvokeAsync<FeatureObjectResultModel<
                            AssignBankToTransaction.AssignBankToTransactionCommandResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            });

        group.MapPost("/{id:guid}/apply-commission",
            async ([FromBody] ApplyTransactionCommission.ApplyTransactionCommissionCommand cmd, IMessageBus bus) =>
            {
                var result =
                    await bus
                        .InvokeAsync<FeatureObjectResultModel<
                            ApplyTransactionCommission.ApplyTransactionCommissionCommandResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            });

        group.MapPost("/{id:guid}/mark-settled",
            async ([FromBody] MarkTransactionAsSettled.MarkTransactionAsSettledCommand cmd, IMessageBus bus) =>
            {
                var result =
                    await bus
                        .InvokeAsync<FeatureObjectResultModel<
                            MarkTransactionAsSettled.MarkTransactionAsSettledCommandResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            });

        return app;
    }
}