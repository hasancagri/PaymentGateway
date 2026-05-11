using IAM.Api.Domains.Users;

namespace PaymentGatewayApi.Modules.IAM.Users.Features.Commands;

public static class AssignUserMerchant
{
    public class AssignUserMerchantCommand
    {
        public required Guid UserId { get; set; }
        public required Guid MerchantId { get; set; }
    }

    public class AssignUserMerchantCommandResponse
    {
    }

    [Transactional]
    public class AssignUserMerchantHandler
    {
        public async Task<FeatureObjectResultModel<AssignUserMerchantCommandResponse>> Handle(
            AssignUserMerchantCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var user = await session.LoadAsync<User>(cmd.UserId, ct);
            if (user is null)
                return FeatureObjectResultModel<AssignUserMerchantCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = user.AssignMerchant(cmd.MerchantId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<AssignUserMerchantCommandResponse>.Error(result.Messages!);

            session.Store(user);
            return FeatureObjectResultModel<AssignUserMerchantCommandResponse>.Ok(new AssignUserMerchantCommandResponse());
        }
    }
}