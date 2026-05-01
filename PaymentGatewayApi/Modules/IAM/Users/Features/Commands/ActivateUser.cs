using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.IAM.Users.Features.Commands;

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
            IamContext db,
            CancellationToken ct)
        {
            var user = await db.Set<User>().FirstOrDefaultAsync(x => x.Id == cmd.UserId, ct);
            if (user is null)
                return FeatureObjectResultModel<ActivateUserCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            user.Activate();
            return FeatureObjectResultModel<ActivateUserCommandResponse>.Ok(new ActivateUserCommandResponse
            {
                Id = user.Id
            });
        }
    }
}