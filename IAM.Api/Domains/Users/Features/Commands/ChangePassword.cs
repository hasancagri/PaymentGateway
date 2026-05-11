namespace PaymentGatewayApi.Modules.IAM.Users.Features.Commands;

public static class ChangePassword
{
    public class ChangePasswordCommand
    {
        public required Guid UserId { get; set; }
        public required string NewPassword { get; set; }
    }

    public class ChangePasswordCommandResponse
    {
    }

    [Transactional]
    public class ChangePasswordHandler
    {
        public async Task<FeatureObjectResultModel<ChangePasswordCommandResponse>> Handle(
            ChangePasswordCommand cmd,
            IamContext db,
            CancellationToken ct)
        {
            var user = await db.Set<User>().FirstOrDefaultAsync(x => x.Id == cmd.UserId, ct);
            if (user is null)
            {
                return FeatureObjectResultModel<ChangePasswordCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });
            }


            var result = user.ChangePassword(cmd.NewPassword);
            if (!result.IsSuccess)
            {
                return FeatureObjectResultModel<ChangePasswordCommandResponse>.Error(result.Messages!);
            }
            
            return FeatureObjectResultModel<ChangePasswordCommandResponse>.Ok(new ChangePasswordCommandResponse());
        }
    }
}