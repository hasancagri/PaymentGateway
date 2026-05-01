using PaymentGatewayApi.Modules.IAM.Roles.Entities;
using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.IAM.Roles.Features.Commands;

public static class RemoveRolePermission
{
    public class RemoveRolePermissionCommand
    {
        public required Guid RoleId { get; set; }
        public required Guid PermissionId { get; set; }
    }
    
    public class RemoveRolePermissionCommandResponse
    {
    }

    [Transactional]
    public class RemoveRolePermissionHandler
    {
        public async Task<FeatureObjectResultModel<RemoveRolePermissionCommandResponse>> Handle(
            RemoveRolePermissionCommand cmd,
            IamContext db,
            CancellationToken ct)
        {
            var role = await db.Set<Role>()
                .Include(x => x.Permissions)
                .FirstOrDefaultAsync(x => x.Id == cmd.RoleId, ct);

            if (role is null)
                return FeatureObjectResultModel<RemoveRolePermissionCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Role),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = role.RemovePermission(cmd.PermissionId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<RemoveRolePermissionCommandResponse>.Error(result.Messages!);

            return FeatureObjectResultModel<RemoveRolePermissionCommandResponse>.Ok(new RemoveRolePermissionCommandResponse());
        }
    }
}