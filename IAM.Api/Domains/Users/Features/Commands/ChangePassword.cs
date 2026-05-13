namespace IAM.Api.Domains.Users.Features.Commands;

public static class ChangePassword
{
    public class ChangePasswordCommand
    {
        public required Guid UserId { get; set; }
        public required string NewPassword { get; set; }
    }

    public class ChangePasswordCommandResponse { }

    [Transactional]
    public class ChangePasswordHandler
    {
        public async Task<FeatureObjectResultModel<ChangePasswordCommandResponse>> Handle(
            ChangePasswordCommand cmd,
            IDocumentSession session,
            IAM.Api.Keycloak.KeycloakAdminClient keycloak,
            CancellationToken ct)
        {
            var user = await session.LoadAsync<User>(cmd.UserId, ct);
            if (user is null)
                return FeatureObjectResultModel<ChangePasswordCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code  = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            await keycloak.ResetPasswordAsync(cmd.UserId, cmd.NewPassword, ct);
            return FeatureObjectResultModel<ChangePasswordCommandResponse>.Ok(
                new ChangePasswordCommandResponse());
        }
    }
}