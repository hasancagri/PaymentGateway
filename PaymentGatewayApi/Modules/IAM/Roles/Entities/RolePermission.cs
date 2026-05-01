using PaymentGatewayApi.Modules.IAM.Roles.Enums;

namespace PaymentGatewayApi.Modules.IAM.Roles.Entities;

public sealed class RolePermission : BaseModel
{
    public string Resource { get; private set; }
    public PermissionType PermissionType { get; private set; }

    private RolePermission()
    {
    } // EF Core

    internal static RolePermission Create(string resource, PermissionType permissionType) => new()
    {
        Resource = resource.Trim(),
        PermissionType = permissionType,
    };
}