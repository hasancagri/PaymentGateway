using Common.Auths;

namespace VenueTalk.Auths;

public static class AuthExtensions
{
    public static void LoadCurrentUser(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddTransient<ICurrentUser>(provider =>
        {
            //get from jwt
            return new CurrentUser
            {
                Id = Guid.NewGuid(),
                Name = "Hasan D.",
                Email = "hasandemiriz@msn.com",
                Phone = "544 999 99 99"
            };
        });
    }
}
