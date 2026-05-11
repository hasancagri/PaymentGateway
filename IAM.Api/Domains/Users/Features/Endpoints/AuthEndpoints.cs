using PaymentGatewayApi.Modules.IAM.Users.Features.Commands;

namespace PaymentGatewayApi.Modules.IAM.Users.Features.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/login", async ([FromBody] Login.LoginCommand cmd, IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<Login.LoginCommandResponse>>(cmd);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        return app;
    }
}