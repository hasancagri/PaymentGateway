namespace PaymentGatewayApi.Modules.IAM.Users.Features.Commands;

public static class DeactivateUser
{
    public class DeactivateUserCommand
    {
        public required Guid UserId { get; set; }
    }

    public class DeactivateUserCommandResponse
    {
        public Guid UserId { get; set; }
    }

    [Transactional]
    public class DeactivateUserHandler
    {
        public async Task<FeatureObjectResultModel<DeactivateUserCommandResponse>> Handle(
            DeactivateUserCommand cmd,
            IamContext db,
            ICache cache,
            CancellationToken ct)
        {
            var user = await db.Set<User>().FirstOrDefaultAsync(x => x.Id == cmd.UserId, ct);
            if (user is null)
                return FeatureObjectResultModel<DeactivateUserCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = user.Deactivate();
            if (!result.IsSuccess)
                return FeatureObjectResultModel<DeactivateUserCommandResponse>.Error(result.Messages!);

            await cache.Remove($"user:{cmd.UserId}");
            return FeatureObjectResultModel<DeactivateUserCommandResponse>.Ok(new DeactivateUserCommandResponse
            {
                UserId = cmd.UserId
            });
        }
    }
}