namespace MerchantManagement.Api.Authorization;

public sealed class JwtPermissionFilter(ICurrentUser currentUser) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (currentUser.Id == Guid.Empty)
            return Results.Unauthorized();

        return await next(context);
    }
}