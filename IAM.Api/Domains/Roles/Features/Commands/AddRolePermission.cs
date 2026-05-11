using PaymentGatewayApi.Modules.IAM.Roles;

namespace IAM.Api.Domains.Roles.Features.Commands;

public static class AddRolePermission
{
    public class AddRolePermissionCommand
    {
        public required Guid RoleId { get; set; }
        public required string PageRoute { get; set; }
        public required string Action { get; set; }
    }

    public class AddRolePermissionCommandResponse
    {
    }

    [Transactional]
    public class AddRolePermissionHandler
    {
        public async Task<FeatureObjectResultModel<AddRolePermissionCommandResponse>> Handle(
            AddRolePermissionCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var role = await session.LoadAsync<Role>(cmd.RoleId, ct);

            if (role is null)
                return FeatureObjectResultModel<AddRolePermissionCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Role),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = role.AddPermission(cmd.PageRoute, cmd.Action);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<AddRolePermissionCommandResponse>.Error(result.Messages!);

            session.Store(role);
            return FeatureObjectResultModel<AddRolePermissionCommandResponse>.Ok(new AddRolePermissionCommandResponse());
        }
    }
}