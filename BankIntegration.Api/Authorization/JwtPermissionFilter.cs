using PaymentGatewayApi.Auths;

namespace PaymentGatewayApi.Authorization;

public sealed class JwtPermissionFilter(ICurrentUser currentUser, ICache cache) 
    : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var metadata = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<JwtPermissionMetadata>();

        if (metadata is null)
            return await next(context);

        if (currentUser.Id == Guid.Empty)
            return Results.Unauthorized();

        var session = await cache.Get<UserSessionCache>($"user:{currentUser.Id}");
        if (session is null)
            return Results.Unauthorized();

        var page = session.Pages.FirstOrDefault(p => p.Route == metadata.Page);
        if (page is null)
            return Results.Forbid();

        return page.Actions.Contains(metadata.Action)
            ? await next(context)
            : Results.Forbid();
    }
}