using CommissionManagement.Api.Authorization;
using CommissionManagement.Api.CommissionManagement.MerchantCommissions.Features.Commands;
using CommissionManagement.Api.Domains.MerchantCommissions.Features.Commands;
using CommissionManagement.Api.Domains.MerchantCommissions.Features.Queries;
using Microsoft.AspNetCore.Mvc;

namespace CommissionManagement.Api.Domains.MerchantCommissions.Features.Endpoints;

public static class MerchantCommissionEndpoints
{
    public static IEndpointRouteBuilder MapMerchantCommissionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/merchant-commissions")
            .AddEndpointFilter<JwtPermissionFilter>();

        group.MapPost("/", async ([FromBody] CreateMerchantCommission.CreateMerchantCommissionCommand cmd, IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateMerchantCommission.CreateMerchantCommissionResponse>>(cmd);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapGet("/", async ([FromQuery] Guid merchantId, [FromQuery] int page, [FromQuery] int pageSize, IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<List<GetMerchantCommissions.MerchantCommissionListItem>>>(
                new GetMerchantCommissions.GetMerchantCommissionsQuery { MerchantId = merchantId, Page = page, PageSize = pageSize });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<GetMerchantCommissionById.GetMerchantCommissionByIdResponse>>(
                new GetMerchantCommissionById.GetMerchantCommissionByIdQuery { CommissionId = id });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapPatch("/{id:guid}/rate", async ([FromBody] UpdateMerchantCommissionRate.UpdateMerchantCommissionRateCommand cmd, IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<UpdateMerchantCommissionRate.UpdateMerchantCommissionRateCommandResponse>>(cmd);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        return app;
    }
}