using PaymentGatewayApi.Modules.IAM.Users;

namespace IAM.Api.Domains.Users.Features.Commands;

public static class ActivateUser
{
    public class ActivateUserCommand
    {
        public required Guid UserId { get; set; }
    }

    public class ActivateUserCommandResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class ActivateUserHandler
    {
        public async Task<FeatureObjectResultModel<ActivateUserCommandResponse>> Handle(
            ActivateUserCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var user = await session.LoadAsync<User>(cmd.UserId, ct);
            if (user is null)
                return FeatureObjectResultModel<ActivateUserCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            user.Activate();
            session.Store(user);
            return FeatureObjectResultModel<ActivateUserCommandResponse>.Ok(new ActivateUserCommandResponse
            {
                Id = user.Id
            });
        }
    }
}