using Common.Utils.Helpers;
using IAM.Api.Auths;
using IAM.Api.Domains.Roles;

namespace IAM.Api.Domains.Users.Features.Commands;

public static class Login
{
    public class LoginCommand
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
    }

    public class LoginCommandResponse
    {
        public string? Token { get; set; }
    }

    [Transactional]
    public class LoginHandler
    {
        public async Task<FeatureObjectResultModel<LoginCommandResponse>> Handle(
            LoginCommand cmd,
            IDocumentSession session,
            IJwtHelper jwtHelper,
            ICache cache,
            CancellationToken ct)
        {
            var user = await session.Query<User>().FirstOrDefaultAsync(x => x.Email.Value == cmd.Email, ct);

            if (user is null || !user.Login(cmd.Password))
            {
                return FeatureObjectResultModel<LoginCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });
            }

            session.Store(user);

            var roleIds = user.Roles.Select(r => r.RoleId).ToList();
            var roles = await session.Query<Role>().Where(x => roleIds.Contains(x.Id)).ToListAsync(ct);

            var pages = roles.SelectMany(r => r.Permissions)
                .GroupBy(p => p.PageRoute)
                .Select(g => new PageAccess
                {
                    Route = g.Key,
                    Actions = g.SelectMany(p => p.Actions.Select(a => a.Action)).Distinct().ToList()
                })
                .ToList();

            var claimInfo = new UserClaimInfo(user.FullName.FirstName, user.FullName.LastName, user.Email.Value);
            var accessToken = jwtHelper.Create(claimInfo);

            await cache.Set($"user:{user.Id}", new UserSessionCache
            {
                UserId = user.Id,
                Pages = pages
            });

            return FeatureObjectResultModel<LoginCommandResponse>.Ok(new LoginCommandResponse
            {
                Token = accessToken.Token
            });
        }
    }
}