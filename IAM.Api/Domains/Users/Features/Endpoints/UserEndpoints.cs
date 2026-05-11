namespace IAM.Api.Domains.Users.Features.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users");

        group.MapPost("/", async ([FromBody] CreateUser.CreateUserCommand cmd, IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateUser.CreateUserResponse>>(cmd);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapGet("/", async (IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<List<GetAllUsers.UserListItem>>>(
                    new GetAllUsers.GetAllUsersQuery());
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<GetUserById.GetUserByIdResponse>>(
                    new GetUserById.GetUserByIdQuery { UserId = id });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapPost("/{id:guid}/activate", async (Guid id, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<ActivateUser.ActivateUserCommandResponse>>(
                    new ActivateUser.ActivateUserCommand { UserId = id });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapPost("/{id:guid}/deactivate", async (Guid id, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<DeactivateUser.DeactivateUserCommandResponse>>(
                    new DeactivateUser.DeactivateUserCommand { UserId = id });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapPost("/{id:guid}/change-password",
            async ([FromBody] ChangePassword.ChangePasswordCommand cmd, IMessageBus bus) =>
            {
                var result =
                    await bus.InvokeAsync<FeatureObjectResultModel<ChangePassword.ChangePasswordCommandResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            });

        group.MapPost("/{id:guid}/roles",
            async ([FromBody] AssignUserRole.AssignUserRoleCommand cmd, IMessageBus bus) =>
            {
                var result =
                    await bus.InvokeAsync<FeatureObjectResultModel<AssignUserRole.AssignUserRoleCommandResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            });

        group.MapDelete("/{id:guid}/roles/{roleId:guid}", async (Guid id, Guid roleId, IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<RevokeUserRole.RevokeUserRoleCommandResponse>>(
                new RevokeUserRole.RevokeUserRoleCommand { UserId = id, RoleId = roleId });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapPost("/{id:guid}/merchant",
            async ([FromBody] AssignUserMerchant.AssignUserMerchantCommand cmd, IMessageBus bus) =>
            {
                var result =
                    await bus.InvokeAsync<FeatureObjectResultModel<AssignUserMerchant.AssignUserMerchantCommandResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            });

        group.MapDelete("/{id:guid}/merchant", async (Guid id, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<RemoveUserFromMerchant.RemoveUserFromMerchantCommandResponse>>(
                    new RemoveUserFromMerchant.RemoveUserFromMerchantCommand { UserId = id });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        return app;
    }
}