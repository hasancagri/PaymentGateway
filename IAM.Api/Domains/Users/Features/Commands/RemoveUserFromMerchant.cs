namespace PaymentGatewayApi.Modules.IAM.Users.Features.Commands;

public static class RemoveUserFromMerchant
{
    public class RemoveUserFromMerchantCommand
    {
        public required Guid UserId { get; set; }
    }

    public class RemoveUserFromMerchantCommandResponse
    {
    }

    [Transactional]
    public class RemoveUserFromMerchantHandler
    {
        public async Task<FeatureObjectResultModel<RemoveUserFromMerchantCommandResponse>> Handle(
            RemoveUserFromMerchantCommand cmd,
            IamContext db,
            CancellationToken ct)
        {
            var user = await db.Set<User>().FirstOrDefaultAsync(x => x.Id == cmd.UserId, ct);
            if (user is null)
                return FeatureObjectResultModel<RemoveUserFromMerchantCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = user.RemoveFromMerchant();
            if (!result.IsSuccess)
                return FeatureObjectResultModel<RemoveUserFromMerchantCommandResponse>.Error(result.Messages!);

            return FeatureObjectResultModel<RemoveUserFromMerchantCommandResponse>.Ok(new RemoveUserFromMerchantCommandResponse());
        }
    }
}