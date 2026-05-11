using IAM.Api.Auths;
using IAM.Api.Domains.Roles;
using PaymentGatewayApi.Modules.IAM.Roles;
using PaymentGatewayApi.Modules.IAM.Users;

namespace IAM.Api.Domains.Users.Features.Commands;

public static class AssignUserRole
{
    public class AssignUserRoleCommand
    {
        public required Guid UserId { get; set; }
        public required Guid RoleId { get; set; }
    }

    public class AssignUserRoleCommandResponse
    {
        
    }

    [Transactional]
    public class AssignUserRoleHandler
    {
        public async Task<FeatureObjectResultModel<AssignUserRoleCommandResponse>> Handle(
            AssignUserRoleCommand cmd,
            IDocumentSession session,
            ICache cache,
            CancellationToken ct)
        {
            var user = await session.LoadAsync<User>(cmd.UserId, ct);
            if (user is null)
                return FeatureObjectResultModel<AssignUserRoleCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = user.AssignRole(cmd.RoleId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<AssignUserRoleCommandResponse>.Error(result.Messages!);

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

            await cache.Set($"user:{cmd.UserId}", new UserSessionCache { UserId = cmd.UserId, Pages = pages });
            return FeatureObjectResultModel<AssignUserRoleCommandResponse>.Ok(new AssignUserRoleCommandResponse());
        }
    }
}