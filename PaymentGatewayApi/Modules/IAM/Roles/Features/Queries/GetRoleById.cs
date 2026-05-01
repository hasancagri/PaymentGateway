using PaymentGatewayApi.Modules.IAM.Roles.Enums;

namespace PaymentGatewayApi.Modules.IAM.Roles.Features.Queries;

public static class GetRoleById
{
    public class GetRoleByIdQuery
    {
        public required Guid RoleId { get; set; }
    }

    public class GetRoleByIdResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public bool IsSystem { get; set; }
        public List<PermissionItem> Permissions { get; set; } = [];
    }

    public class PermissionItem
    {
        public Guid Id { get; set; }
        public string Resource { get; set; }
        public PermissionType PermissionType { get; set; }
    }

    public class GetRoleByIdHandler
    {
        public async Task<FeatureObjectResultModel<GetRoleByIdResponse>> Handle(
            GetRoleByIdQuery query,
            IamContext db,
            CancellationToken ct)
        {
            var role = await db.Set<Role>()
                .Include(x => x.Permissions)
                .FirstOrDefaultAsync(x => x.Id == query.RoleId, ct);

            if (role is null)
                return FeatureObjectResultModel<GetRoleByIdResponse>.Error(new MessageItem
                {
                    Table = nameof(Role),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            return FeatureObjectResultModel<GetRoleByIdResponse>.Ok(new GetRoleByIdResponse
            {
                Id = role.Id,
                Name = role.Name.Value,
                IsSystem = role.IsSystem,
                Permissions = role.Permissions.Select(p => new PermissionItem
                {
                    Id = p.Id,
                    Resource = p.Resource,
                    PermissionType = p.PermissionType
                }).ToList()
            });
        }
    }
}