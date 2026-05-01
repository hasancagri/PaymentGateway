using Common.Utils.Helpers;
using PaymentGatewayApi.Auths;
using PaymentGatewayApi.Modules.IAM.Roles;

namespace PaymentGatewayApi.Modules.IAM.Users.Features.Commands;

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
            IamContext db,
            IJwtHelper jwtHelper,
            ICache cache,
            CancellationToken ct)
        {
            var user = await db.Set<User>()
                .Include(x => x.Roles)
                .FirstOrDefaultAsync(x => x.Email.Value == cmd.Email, ct);

            if (user is null)
            {
                return FeatureObjectResultModel<LoginCommandResponse>.Error(new MessageItem
                {
                    //TODO: Buraya geleceğim
                    Table = nameof(User),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_DUPLICATE
                });
            }

            if (!user.Password.Verify(cmd.Password))
            {
                return FeatureObjectResultModel<LoginCommandResponse>.Error(new MessageItem
                {
                    //TODO: Buraya geleceğim
                    Table = nameof(User),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_DUPLICATE
                });
            }

            var roleIds = user.Roles.Select(r => r.RoleId).ToList();
            var permissions = await db.Set<Role>()
                .Where(x => roleIds.Contains(x.Id))
                .SelectMany(x => x.Permissions)
                .Select(x => new PermissionCache
                {
                    Resource = x.Resource, 
                    PermissionType = x.PermissionType
                })
                .ToListAsync(ct);

            var claimInfo = new UserClaimInfo(user.FullName.FirstName, user.FullName.LastName, user.Email.Value);
            var accessToken = jwtHelper.Create(claimInfo);

            await cache.Set($"user:{user.Id}", new UserSessionCache
            {
                UserId = user.Id,
                Permissions = permissions
            });

            return FeatureObjectResultModel<LoginCommandResponse>.Ok(new LoginCommandResponse
            {
                Token = accessToken.Token
            });
        }
    }
}