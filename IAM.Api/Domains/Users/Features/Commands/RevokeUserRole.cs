namespace IAM.Api.Domains.Users.Features.Commands;

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
            return FeatureObjectResultModel<RevokeUserRoleCommandResponse>.Ok(new RevokeUserRoleCommandResponse());
        }
    }
}