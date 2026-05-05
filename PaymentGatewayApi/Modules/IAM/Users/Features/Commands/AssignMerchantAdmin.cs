using PaymentGatewayApi.Modules.IAM.Roles;

namespace PaymentGatewayApi.Modules.IAM.Users.Features.Commands;

public static class AssignMerchantAdmin
{
    private const string MerchantAdminRoleName = "MerchantAdmin";

    public class AssignMerchantAdminCommand
    {
        public required Guid UserId { get; set; }
        public required Guid MerchantId { get; set; }
    }

    public class AssignMerchantAdminCommandResponse
    {
    }

    [Transactional]
    public class AssignMerchantAdminHandler
    {
        public async Task<FeatureObjectResultModel<AssignMerchantAdminCommandResponse>> Handle(
            AssignMerchantAdminCommand cmd,
            IamContext db,
            CancellationToken ct)
        {
            var user = await db.Set<User>()
                .Include(x => x.Roles)
                .FirstOrDefaultAsync(x => x.Id == cmd.UserId, ct);
            if (user is null)
                return FeatureObjectResultModel<AssignMerchantAdminCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var merchantAdminRole = await db.Set<Role>()
                .FirstOrDefaultAsync(r => r.Name.Value == MerchantAdminRoleName, ct);
            if (merchantAdminRole is null)
                return FeatureObjectResultModel<AssignMerchantAdminCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Role),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var merchantResult = user.AssignMerchant(cmd.MerchantId);
            if (!merchantResult.IsSuccess)
                return FeatureObjectResultModel<AssignMerchantAdminCommandResponse>.Error(merchantResult.Messages!);

            var roleResult = user.AssignRole(merchantAdminRole.Id);
            if (!roleResult.IsSuccess)
                return FeatureObjectResultModel<AssignMerchantAdminCommandResponse>.Error(roleResult.Messages!);

            return FeatureObjectResultModel<AssignMerchantAdminCommandResponse>.Ok(new AssignMerchantAdminCommandResponse());
        }
    }
}