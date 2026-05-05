using PaymentGatewayApi.Authorization;
using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.Features.Commands;
using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.Features.Queries;

namespace PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.Features.Endpoints;

public static class BankCommissionEndpoints
{
    public static IEndpointRouteBuilder MapBankCommissionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/bank-commissions")
            .AddEndpointFilter<JwtPermissionFilter>();

        group.MapPost("/", async ([FromBody] DefineBankCommission.DefineBankCommissionCommand cmd, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<DefineBankCommission.DefineBankCommissionResponse>>(cmd);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata(CommissionPermissionConstants.Page, CommissionPermissionConstants.Create));

        group.MapGet("/", async ([FromQuery] Guid? bankId, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<List<GetBankCommissions.BankCommissionListItem>>>(
                    new GetBankCommissions.GetBankCommissionsQuery { BankId = bankId });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata(CommissionPermissionConstants.Page, CommissionPermissionConstants.Read));

        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<GetBankCommissionById.GetBankCommissionByIdResponse>>(
                    new GetBankCommissionById.GetBankCommissionByIdQuery { CommissionId = id });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata(CommissionPermissionConstants.Page, CommissionPermissionConstants.Read));

        group.MapPatch("/{id:guid}/rate",
            async ([FromBody] UpdateBankCommissionRate.UpdateBankCommissionRateCommand cmd, IMessageBus bus) =>
            {
                var result =
                    await bus
                        .InvokeAsync<FeatureObjectResultModel<
                            UpdateBankCommissionRate.UpdateBankCommissionRateCommandResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            }).WithMetadata(new JwtPermissionMetadata(CommissionPermissionConstants.Page, CommissionPermissionConstants.Update));

        return app;
    }
}