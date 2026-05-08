using PaymentGatewayApi.Modules.BankIntegration.MerchantBanks.Features.Commands;

namespace PaymentGatewayApi.Modules.BankIntegration.MerchantBanks.Features.Endpoints;

public static class MerchantBankEndpoints
{
    public static IEndpointRouteBuilder MapMerchantBankEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/banks/{bankId:guid}/merchant-credentials");

        group.MapPost("/", async (Guid bankId, [FromBody] AssignMerchantBank.AssignMerchantBankCommand cmd, IMessageBus bus) =>
        {
            cmd.BankId = bankId;
            var result = await bus.InvokeAsync<FeatureObjectResultModel<AssignMerchantBank.AssignMerchantBankResponse>>(cmd);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        return app;
    }
}