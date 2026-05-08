using MerchantManagement.Api.Modules.MerchantManagement.Merchants.Features.Commands;
using MerchantManagement.Api.Modules.MerchantManagement.Merchants.Features.Queries;

namespace MerchantManagement.Api.Modules.MerchantManagement.Merchants.Features.Endpoints;

public static class MerchantEndpoints
{
    private record ReasonRequest(string Reason);

    public static IEndpointRouteBuilder MapMerchantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/merchants")
            .AddEndpointFilter<JwtPermissionFilter>();

        group.MapPost("/", async ([FromBody] CreateMerchant.CreateMerchantCommand cmd, IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateMerchant.CreateMerchantResponse>>(cmd);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata(MerchantPermissionConstants.Page, MerchantPermissionConstants.Create));

        group.MapGet("/", async (IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<List<GetAllMerchants.MerchantListItem>>>(
                    new GetAllMerchants.GetAllMerchantsQuery());
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata(MerchantPermissionConstants.Page, MerchantPermissionConstants.Read));

        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<GetMerchantById.GetMerchantByIdResponse>>(
                    new GetMerchantById.GetMerchantByIdQuery { MerchantId = id });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata(MerchantPermissionConstants.Page, MerchantPermissionConstants.Read));

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] UpdateMerchant.UpdateMerchantCommand cmd, IMessageBus bus) =>
        {
            cmd.MerchantId = id;
            var result = await bus.InvokeAsync<FeatureObjectResultModel<UpdateMerchant.UpdateMerchantCommandResponse>>(cmd);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata(MerchantPermissionConstants.Page, MerchantPermissionConstants.Update));

        group.MapPost("/{id:guid}/activate", async (Guid id, [FromBody] ReasonRequest req, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<ActivateMerchant.ActivateMerchantCommandResponse>>(
                    new ActivateMerchant.ActivateMerchantCommand { MerchantId = id, Reason = req.Reason });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata(MerchantPermissionConstants.Page, MerchantPermissionConstants.Activate));

        group.MapPost("/{id:guid}/deactivate", async (Guid id, [FromBody] ReasonRequest req, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<DeactivateMerchant.DeactivateMerchantCommandResponse>>(
                    new DeactivateMerchant.DeactivateMerchantCommand { MerchantId = id, Reason = req.Reason });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata(MerchantPermissionConstants.Page, MerchantPermissionConstants.Deactivate));

        group.MapPost("/{id:guid}/suspend", async (Guid id, [FromBody] ReasonRequest req, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<SuspendMerchant.SuspendMerchantCommandResponse>>(
                    new SuspendMerchant.SuspendMerchantCommand { MerchantId = id, Reason = req.Reason });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata(MerchantPermissionConstants.Page, MerchantPermissionConstants.Suspend));

        group.MapPost("/{id:guid}/bank-accounts",
            async (Guid id, [FromBody] AddMerchantBankAccount.AddMerchantBankAccountCommand cmd, IMessageBus bus) =>
            {
                cmd.MerchantId = id;
                var result =
                    await bus
                        .InvokeAsync<FeatureObjectResultModel<
                            AddMerchantBankAccount.AddMerchantBankAccountCommandResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            }).WithMetadata(new JwtPermissionMetadata(MerchantPermissionConstants.Page, MerchantPermissionConstants.BankAccountsAdd));

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
            }).WithMetadata(new JwtPermissionMetadata(MerchantPermissionConstants.Page, MerchantPermissionConstants.BankAccountsRemove));

        group.MapPost("/{id:guid}/api-keys", async (Guid id, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<GenerateApiKey.GenerateApiKeyResponse>>(
                    new GenerateApiKey.GenerateApiKeyCommand { MerchantId = id });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata(MerchantPermissionConstants.Page, MerchantPermissionConstants.ApiKeysGenerate));

        group.MapDelete("/{id:guid}/api-keys/{apiKeyId:guid}", async (Guid id, Guid apiKeyId, IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<RevokeApiKey.RevokeApiKeyCommandResponse>>(
                new RevokeApiKey.RevokeApiKeyCommand { MerchantId = id, ApiKeyId = apiKeyId });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata(MerchantPermissionConstants.Page, MerchantPermissionConstants.ApiKeysRevoke));

        return app;
    }
}