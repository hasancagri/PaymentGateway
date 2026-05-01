using PaymentGatewayApi.Auths;
using PaymentGatewayApi.Modules.IAM.Roles;

namespace PaymentGatewayApi.Modules.IAM.Users.Features.Commands;

public static class RevokeUserRole
{
    public class RevokeUserRoleCommand
    {
        public required Guid UserId { get; set; }
        public required Guid RoleId { get; set; }
    }

    public class RevokeUserRoleCommandResponse
    {
    }

    [Transactional]
    public class RevokeUserRoleHandler
    {
        public async Task<FeatureObjectResultModel<RevokeUserRoleCommandResponse>> Handle(
            RevokeUserRoleCommand cmd,
            IamContext db,
            ICache cache,
            CancellationToken ct)
        {
            var user = await db.Set<User>()
                .Include(x => x.Roles)
                .FirstOrDefaultAsync(x => x.Id == cmd.UserId, ct);

            if (user is null)
                return FeatureObjectResultModel<RevokeUserRoleCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = user.RemoveRole(cmd.RoleId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<RevokeUserRoleCommandResponse>.Error(result.Messages!);

            var roleIds = user.Roles.Select(r => r.RoleId).ToList();
            var permissions = await db.Set<Role>()
                .Where(x => roleIds.Contains(x.Id))
                .SelectMany(x => x.Permissions)
                .Select(x => new PermissionCache { Resource = x.Resource, PermissionType = x.PermissionType })
                .ToListAsync(ct);

            await cache.Set($"user:{cmd.UserId}", new UserSessionCache
            {
                UserId = cmd.UserId, Permissions = permissions
            });
            return FeatureObjectResultModel<RevokeUserRoleCommandResponse>.Ok(new RevokeUserRoleCommandResponse());
        }
    }
}