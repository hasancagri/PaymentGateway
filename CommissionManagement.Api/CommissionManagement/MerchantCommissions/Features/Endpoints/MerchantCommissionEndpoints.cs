using PaymentGatewayApi.Authorization;
using PaymentGatewayApi.Modules.CommissionManagement.MerchantCommissions.Features.Commands;
using PaymentGatewayApi.Modules.CommissionManagement.MerchantCommissions.Features.Queries;

namespace PaymentGatewayApi.Modules.CommissionManagement.MerchantCommissions.Features.Endpoints;

public static class MerchantCommissionEndpoints
{
    public static IEndpointRouteBuilder MapMerchantCommissionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/merchant-commissions")
            .AddEndpointFilter<JwtPermissionFilter>();

        group.MapPost("/",
            async ([FromBody] DefineMerchantCommission.DefineMerchantCommissionCommand cmd, IMessageBus bus) =>
            {
                var result =
                    await bus
                        .InvokeAsync<
                            FeatureObjectResultModel<DefineMerchantCommission.DefineMerchantCommissionResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            }).WithMetadata(new JwtPermissionMetadata(CommissionPermissionConstants.Page, CommissionPermissionConstants.Create));

        group.MapGet("/",
            async ([FromQuery] Guid merchantId, [FromQuery] int page, [FromQuery] int pageSize, IMessageBus bus) =>
            {
                var result =
                    await
                        bus.InvokeAsync<FeatureObjectResultModel<
                            List<GetMerchantCommissions.MerchantCommissionListItem>>>(
                            new GetMerchantCommissions.GetMerchantCommissionsQuery
                                { MerchantId = merchantId, Page = page, PageSize = pageSize });
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            }).WithMetadata(new JwtPermissionMetadata(CommissionPermissionConstants.Page, CommissionPermissionConstants.Read));


        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            var result =
                await bus
                    .InvokeAsync<FeatureObjectResultModel<GetMerchantCommissionById.GetMerchantCommissionByIdResponse>>(
                        new GetMerchantCommissionById.GetMerchantCommissionByIdQuery { CommissionId = id });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata(CommissionPermissionConstants.Page, CommissionPermissionConstants.Read));

        group.MapPatch("/{id:guid}/rate",
            async ([FromBody] UpdateMerchantCommissionRate.UpdateMerchantCommissionRateCommand cmd, IMessageBus bus) =>
            {
                var result =
                    await bus
                        .InvokeAsync<FeatureObjectResultModel<
                            UpdateMerchantCommissionRate.UpdateMerchantCommissionRateCommandResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            }).WithMetadata(new JwtPermissionMetadata(CommissionPermissionConstants.Page, CommissionPermissionConstants.Update));

        return app;
    }
}