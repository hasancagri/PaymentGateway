using PaymentGatewayApi.Modules.BankIntegration.Banks.Features.Commands;
using PaymentGatewayApi.Modules.BankIntegration.Banks.Features.Queries;

namespace PaymentGatewayApi.Modules.BankIntegration.Banks.Features.Endpoints;

public static class BankEndpoints
{
    public static IEndpointRouteBuilder MapBankEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/banks");

        group.MapPost("/", async ([FromBody] ConfigureBank.ConfigureBankCommand cmd, IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<ConfigureBank.ConfigureBankResponse>>(cmd);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapGet("/", async (IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<List<GetAllBanks.BankListItem>>>(
                    new GetAllBanks.GetAllBanksQuery());
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<GetBankById.GetBankByIdResponse>>(
                    new GetBankById.GetBankByIdQuery { BankId = id });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapPut("/{id:guid}", async ([FromBody] UpdateBank.UpdateBankCommand cmd, IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<UpdateBank.UpdateBankCommandResponse>>(cmd);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapPost("/{id:guid}/activate", async (Guid id, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<ActivateBank.ActivateBankCommandResponse>>(
                    new ActivateBank.ActivateBankCommand { BankId = id });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapPost("/{id:guid}/deactivate", async (Guid id, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<DeactivateBank.DeactivateBankCommandResponse>>(
                    new DeactivateBank.DeactivateBankCommand { BankId = id });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapPost("/{id:guid}/currencies",
            async ([FromBody] AddBankCurrency.AddBankCurrencyCommand cmd, IMessageBus bus) =>
            {
                var result =
                    await bus
                        .InvokeAsync<FeatureObjectResultModel<AddBankCurrency.AddBankCurrencyCommandResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            });

        return app;
    }
}