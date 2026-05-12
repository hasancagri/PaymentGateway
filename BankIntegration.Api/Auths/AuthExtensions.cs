namespace BankIntegration.Api.Auths;

public static class AuthExtensions
{
    public static void LoadCurrentUser(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddTransient<ICurrentUser>(provider =>
        {
            var httpContext = provider.GetRequiredService<IHttpContextAccessor>().HttpContext;
            var authHeader = httpContext?.Request.Headers.Authorization.FirstOrDefault();

            if (string.IsNullOrEmpty(authHeader))
                return new CurrentUser();

            try
            {
                return CurrentUser.Load(authHeader);
            }
            catch
            {
                return new CurrentUser();
            }
        });
    }
}