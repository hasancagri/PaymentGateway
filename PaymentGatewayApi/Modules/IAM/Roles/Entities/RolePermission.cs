using PaymentGatewayApi.Modules.IAM.Roles.Enums;

namespace PaymentGatewayApi.Modules.IAM.Roles.Entities;

public sealed class RolePermission
{
    public Guid Id { get; private set; }
    public string Resource { get; private set; }
    public PermissionType PermissionType { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private RolePermission()
    {
    } // EF Core

    internal static RolePermission Create(string resource, PermissionType permissionType) => new()
    {
        Id = Guid.NewGuid(),
        Resource = resource.Trim(),
        PermissionType = permissionType,
        CreatedAt = DateTime.UtcNow
    };
}