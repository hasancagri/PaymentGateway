using PaymentGatewayApi.Modules.IAM.Roles.Entities;
using PaymentGatewayApi.Modules.IAM.Roles.Enums;
using PaymentGatewayApi.Modules.IAM.Roles.ValueObjects;

namespace PaymentGatewayApi.Modules.IAM.Roles;

public sealed class Role : AggregateRoot
{
    public RoleName Name { get; private set; }
    public bool IsSystem { get; private set; }

    private readonly List<RolePermission> _permissions = [];
    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();

    private Role()
    {
    }

    public static ResultDomain<Role> Create(string name, bool isSystem = false)
    {
        var nameResult = RoleName.Create(name);
        if (!nameResult.IsSuccess) return ResultDomain<Role>.Error(nameResult.Messages!);

        return ResultDomain<Role>.Ok(new Role { Name = nameResult.Data!, IsSystem = isSystem });
    }

    public ResultDomain AddPermission(string resource, PermissionType permissionType)
    {
        if (_permissions.Any(p => p.Resource == resource && p.PermissionType == permissionType))
            return ResultDomain.Error(new MessageItem { Code = "RolePermission.AlreadyExists" });

        _permissions.Add(RolePermission.Create(resource, permissionType));
        return ResultDomain.Ok();
    }

    public ResultDomain RemovePermission(Guid permissionId)
    {
        var permission = _permissions.SingleOrDefault(p => p.Id == permissionId);
        if (permission is null)
            return ResultDomain.Error(new MessageItem { Code = "RolePermission.NotFound" });

        _permissions.Remove(permission);
        return ResultDomain.Ok();
    }

    public bool HasPermission(string resource, PermissionType permissionType) =>
        _permissions.Any(p => p.Resource == resource && p.PermissionType == permissionType);
}