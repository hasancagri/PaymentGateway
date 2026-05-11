using IAM.Api.Authorization;
using IAM.Api.Domains.Roles.Features.Commands;
using IAM.Api.Domains.Roles.Features.Queries;
using PaymentGatewayApi.Modules.IAM.Roles.Features.Queries;

namespace IAM.Api.Domains.Roles.Features.Endpoints;

public static class RoleEndpoints
{
    public static IEndpointRouteBuilder MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/roles")
            .AddEndpointFilter<JwtPermissionFilter>();

        group.MapPost("/", async ([FromBody] CreateRole.CreateRoleCommand cmd, IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateRole.CreateRoleResponse>>(cmd);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapGet("/", async (IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<List<GetAllRoles.RoleListItem>>>(
                new GetAllRoles.GetAllRolesQuery());
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<GetRoleById.GetRoleByIdResponse>>(
                new GetRoleById.GetRoleByIdQuery { RoleId = id });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        return app;
    }
}