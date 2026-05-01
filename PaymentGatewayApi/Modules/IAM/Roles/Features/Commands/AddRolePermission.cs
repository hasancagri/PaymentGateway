using PaymentGatewayApi.Modules.IAM.Roles.Enums;
using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.IAM.Roles.Features.Commands;

public static class AddRolePermission
{
    public class AddRolePermissionCommand
    {
        public required Guid RoleId { get; set; }
        public required string Resource { get; set; }
        public required PermissionType PermissionType { get; set; }
    }
    
    public class AddRolePermissionCommandResponse
    {
    }

    [Transactional]
    public class AddRolePermissionHandler
    {
        public async Task<FeatureObjectResultModel<AddRolePermissionCommandResponse>> Handle(
            AddRolePermissionCommand cmd,
            IamContext db,
            CancellationToken ct)
        {
            var role = await db.Set<Role>().FirstOrDefaultAsync(x => x.Id == cmd.RoleId, ct);
            if (role is null)
                return FeatureObjectResultModel<AddRolePermissionCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Role),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = role.AddPermission(cmd.Resource, cmd.PermissionType);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<AddRolePermissionCommandResponse>.Error(result.Messages!);

            return FeatureObjectResultModel<AddRolePermissionCommandResponse>.Ok(new AddRolePermissionCommandResponse());
        }
    }
}