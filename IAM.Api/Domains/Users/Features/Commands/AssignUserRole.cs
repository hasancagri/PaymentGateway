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
            return FeatureObjectResultModel<AssignUserRoleCommandResponse>.Ok(new AssignUserRoleCommandResponse());
        }
    }
}