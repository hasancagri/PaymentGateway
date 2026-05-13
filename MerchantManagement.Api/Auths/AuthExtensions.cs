namespace MerchantManagement.Api.Auths;

public static class AuthExtensions
{
    public static void LoadCurrentUser(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddTransient<ICurrentUser>(provider =>
        {
            var httpContext = provider.GetRequiredService<IHttpContextAccessor>().HttpContext;
            var principal = httpContext?.User;

            if (principal?.Identity?.IsAuthenticated != true)
                return new CurrentUser();

            return CurrentUser.Load(principal);
        });
    }
}