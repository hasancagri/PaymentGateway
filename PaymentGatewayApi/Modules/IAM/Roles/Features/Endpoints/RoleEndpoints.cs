using PaymentGatewayApi.Modules.IAM.Roles.Features.Commands;
using PaymentGatewayApi.Modules.IAM.Roles.Features.Queries;

namespace PaymentGatewayApi.Modules.IAM.Roles.Features.Endpoints;

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
        }).WithMetadata(new JwtPermissionMetadata(RolePermissionConstants.Page, RolePermissionConstants.Create));

        group.MapGet("/", async (IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<List<GetAllRoles.RoleListItem>>>(
                    new GetAllRoles.GetAllRolesQuery());
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata(RolePermissionConstants.Page, RolePermissionConstants.Read));

        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<GetRoleById.GetRoleByIdResponse>>(
                    new GetRoleById.GetRoleByIdQuery { RoleId = id });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).WithMetadata(new JwtPermissionMetadata(RolePermissionConstants.Page, RolePermissionConstants.Read));

        group.MapPost("/{id:guid}/permissions",
            async ([FromBody] AddRolePermission.AddRolePermissionCommand cmd, IMessageBus bus) =>
            {
                var result =
                    await bus.InvokeAsync<FeatureObjectResultModel<AddRolePermission.AddRolePermissionCommandResponse>>(
                        cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            }).WithMetadata(new JwtPermissionMetadata(RolePermissionConstants.Page, RolePermissionConstants.PermissionsAdd));

        group.MapDelete("/{id:guid}/permissions/{permissionId:guid}",
            async (Guid id, Guid permissionId, IMessageBus bus) =>
            {
                var result =
                    await bus
                        .InvokeAsync<
                            FeatureObjectResultModel<RemoveRolePermission.RemoveRolePermissionCommandResponse>>(
                            new RemoveRolePermission.RemoveRolePermissionCommand
                                { RoleId = id, PermissionId = permissionId });
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            }).WithMetadata(new JwtPermissionMetadata(RolePermissionConstants.Page, RolePermissionConstants.PermissionsRemove));

        return app;
    }
}