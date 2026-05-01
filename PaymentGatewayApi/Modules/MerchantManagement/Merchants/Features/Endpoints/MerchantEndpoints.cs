using PaymentGatewayApi.Authorization;
using PaymentGatewayApi.Modules.MerchantManagement.Merchants.Features.Commands;
using PaymentGatewayApi.Modules.MerchantManagement.Merchants.Features.Queries;

namespace PaymentGatewayApi.Modules.MerchantManagement.Merchants.Features.Endpoints;

public static class MerchantEndpoints
{
    private record ReasonRequest(string Reason);
    private record CurrencyRequest(string Currency);

    public static IEndpointRouteBuilder MapMerchantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/merchants")
            .AddEndpointFilter<JwtPermissionFilter>();

        group.MapPost("/", async ([FromBody] CreateMerchant.CreateMerchantCommand cmd, IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateMerchant.CreateMerchantResponse>>(cmd);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata("merchants:create"));

        group.MapGet("/", async (IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<List<GetAllMerchants.MerchantListItem>>>(
                    new GetAllMerchants.GetAllMerchantsQuery());
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata("merchants:read"));

        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<GetMerchantById.GetMerchantByIdResponse>>(
                    new GetMerchantById.GetMerchantByIdQuery { MerchantId = id });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata("merchants:read"));

        group.MapPut("/{id:guid}", async ([FromBody] UpdateMerchant.UpdateMerchantCommand cmd, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<UpdateMerchant.UpdateMerchantCommandResponse>>(cmd);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata("merchants:update"));

        group.MapPost("/{id:guid}/activate", async (Guid id, [FromBody] ReasonRequest req, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<ActivateMerchant.ActivateMerchantCommandResponse>>(
                    new ActivateMerchant.ActivateMerchantCommand { MerchantId = id, Reason = req.Reason });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata("merchants:activate"));

        group.MapPost("/{id:guid}/deactivate", async (Guid id, [FromBody] ReasonRequest req, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<DeactivateMerchant.DeactivateMerchantCommandResponse>>(
                    new DeactivateMerchant.DeactivateMerchantCommand { MerchantId = id, Reason = req.Reason });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata("merchants:deactivate"));

        group.MapPost("/{id:guid}/suspend", async (Guid id, [FromBody] ReasonRequest req, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<SuspendMerchant.SuspendMerchantCommandResponse>>(
                    new SuspendMerchant.SuspendMerchantCommand { MerchantId = id, Reason = req.Reason });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata("merchants:suspend"));

        group.MapPost("/{id:guid}/bank-accounts",
            async ([FromBody] AddMerchantBankAccount.AddMerchantBankAccountCommand cmd, IMessageBus bus) =>
            {
                var result =
                    await bus
                        .InvokeAsync<FeatureObjectResultModel<
                            AddMerchantBankAccount.AddMerchantBankAccountCommandResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            }).WithMetadata(new JwtPermissionMetadata("merchants:bank-accounts:add"));

        group.MapDelete("/{id:guid}/bank-accounts/{bankAccountId:guid}",
            async (Guid id, Guid bankAccountId, IMessageBus bus) =>
            {
                var result =
                    await bus
                        .InvokeAsync<FeatureObjectResultModel<
                            RemoveMerchantBankAccount.RemoveMerchantBankAccountCommandResponse>>(
                            new RemoveMerchantBankAccount.RemoveMerchantBankAccountCommand
                                { MerchantId = id, BankAccountId = bankAccountId });
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            }).WithMetadata(new JwtPermissionMetadata("merchants:bank-accounts:remove"));

        group.MapPost("/{id:guid}/currencies",
            async ([FromBody] AddMerchantCurrency.AddMerchantCurrencyCommand cmd, IMessageBus bus) =>
            {
                var result =
                    await bus
                        .InvokeAsync<FeatureObjectResultModel<AddMerchantCurrency.AddMerchantCurrencyCommandResponse>>(
                            cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            }).WithMetadata(new JwtPermissionMetadata("merchants:currencies:add"));

        group.MapDelete("/{id:guid}/currencies", async (Guid id, [FromBody] CurrencyRequest req, IMessageBus bus) =>
        {
            var result =
                await bus
                    .InvokeAsync<
                        FeatureObjectResultModel<RemoveMerchantCurrency.RemoveMerchantCurrencyCommandResponse>>(
                        new RemoveMerchantCurrency.RemoveMerchantCurrencyCommand
                            { MerchantId = id, Currency = req.Currency });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata("merchants:currencies:remove"));

        group.MapPost("/{id:guid}/api-keys", async (Guid id, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<GenerateApiKey.GenerateApiKeyResponse>>(
                    new GenerateApiKey.GenerateApiKeyCommand { MerchantId = id });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata("merchants:api-keys:generate"));

        group.MapDelete("/{id:guid}/api-keys/{apiKeyId:guid}", async (Guid id, Guid apiKeyId, IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<RevokeApiKey.RevokeApiKeyCommandResponse>>(
                new RevokeApiKey.RevokeApiKeyCommand { MerchantId = id, ApiKeyId = apiKeyId });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata("merchants:api-keys:revoke"));

        return app;
    }
}