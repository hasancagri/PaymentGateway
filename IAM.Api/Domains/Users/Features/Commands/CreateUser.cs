namespace IAM.Api.Domains.Users.Features.Commands;

public static class CreateUser
{
    public class CreateUserCommand
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
    }

    public class CreateUserResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateUserHandler
    {
        public async Task<FeatureObjectResultModel<CreateUserResponse>> Handle(
            CreateUserCommand cmd,
            IDocumentSession session,
            IAM.Api.Keycloak.KeycloakAdminClient keycloak,
            CancellationToken ct)
        {
            var emailExists = await session.Query<User>()
                .AnyAsync(x => x.Email.Value == cmd.Email, ct);

            if (emailExists)
                return FeatureObjectResultModel<CreateUserResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code  = CommonResourceConstants.COMMON_MESSAGE_RECORD_DUPLICATE
                });

            Guid keycloakId;
            try
            {
                keycloakId = await keycloak.CreateUserAsync(
                    cmd.Email, cmd.Password, cmd.FirstName, cmd.LastName, ct);
            }
            catch (Exception)
            {
                return FeatureObjectResultModel<CreateUserResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code  = "User.KeycloakCreateFailed"
                });
            }

            var userResult = User.Create(keycloakId, cmd.Email, cmd.FirstName, cmd.LastName);
            if (!userResult.IsSuccess)
            {
                await keycloak.DeleteUserAsync(keycloakId, ct);
                return FeatureObjectResultModel<CreateUserResponse>.Error(userResult.Messages!);
            }

            session.Store(userResult.Data!);
            return FeatureObjectResultModel<CreateUserResponse>.Ok(
                new CreateUserResponse { Id = userResult.Data!.Id });
        }
    }
}