namespace IAM.Api.Auths;

public static class AuthExtensions
{
    public static void LoadCurrentUser(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddTransient<ICurrentUser>(provider =>
        {
            var httpContext = provider
                .GetRequiredService<IHttpContextAccessor>().HttpContext;

            if (httpContext?.User?.Identity?.IsAuthenticated != true)
                return new CurrentUser();

            return CurrentUser.Load(httpContext.User);
        });
    }
}