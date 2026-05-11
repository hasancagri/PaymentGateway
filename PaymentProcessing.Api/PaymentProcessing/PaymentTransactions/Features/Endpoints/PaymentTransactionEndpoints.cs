using PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Features.Commands;

namespace PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Features.Endpoints;

public static class PaymentTransactionEndpoints
{
    public static IEndpointRouteBuilder MapPaymentTransactionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/payments");

        group.MapPost("/auth", async ([FromBody] AuthPayment.AuthPaymentCommand cmd, IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<AuthPayment.AuthPaymentResponse>>(cmd);
            return result.IsSuccess ? Results.Accepted(null, result) : Results.BadRequest(result);
        });

        return app;
    }
}