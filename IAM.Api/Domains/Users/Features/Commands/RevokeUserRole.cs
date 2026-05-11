using IAM.Api.Auths;
using IAM.Api.Domains.Roles;
using IAM.Api.Domains.Users;
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
            IDocumentSession session,
            ICache cache,
            CancellationToken ct)
        {
            var user = await session.LoadAsync<User>(cmd.UserId, ct);

            if (user is null)
                return FeatureObjectResultModel<RevokeUserRoleCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = user.RemoveRole(cmd.RoleId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<RevokeUserRoleCommandResponse>.Error(result.Messages!);

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

            await cache.Set($"user:{cmd.UserId}", new UserSessionCache
            {
                UserId = cmd.UserId, Pages = pages
            });
            return FeatureObjectResultModel<RevokeUserRoleCommandResponse>.Ok(new RevokeUserRoleCommandResponse());
        }
    }
}