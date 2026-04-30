using PaymentGatewayApi.Modules.IAM.Roles.Entities;
using PaymentGatewayApi.Modules.IAM.Roles.Enums;
using PaymentGatewayApi.Modules.IAM.Roles.Events;
using PaymentGatewayApi.Modules.IAM.Roles.ValueObjects;

namespace PaymentGatewayApi.Modules.IAM.Roles;

public sealed class Role : AggregateRoot
{
    // ── Identity ──────────────────────────────────────────
    public RoleId Id { get; private set; }
    public RoleName Name { get; private set; }
    public bool IsSystem { get; private set; } // Seed rollerdir, silinemez

    // ── Collections ───────────────────────────────────────
    private readonly List<RolePermission> _permissions = [];
    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();

    // ── Audit ─────────────────────────────────────────────
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Role()
    {
    } // EF Core

    // ── Factory ───────────────────────────────────────────
    public static Role Create(RoleName name, bool isSystem = false)
    {
        var role = new Role
        {
            Id = RoleId.New(),
            Name = name,
            IsSystem = isSystem,
            CreatedAt = DateTime.UtcNow
        };

        role.RaiseDomainEvent(new RoleCreated(
            Guid.NewGuid(), DateTime.UtcNow,
            role.Id.Value, role.Name.Value));

        return role;
    }

    // ── Permissions ───────────────────────────────────────
    public void AddPermission(string resource, PermissionType permissionType)
    {
        if (_permissions.Any(p => p.Resource == resource && p.PermissionType == permissionType))
            throw new DomainException($"Permission '{permissionType}' on '{resource}' already exists.");

        var permission = RolePermission.Create(resource, permissionType);
        _permissions.Add(permission);
        Touch();

        RaiseDomainEvent(new RolePermissionAdded(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, resource, permissionType.ToString()));
    }

    public void RemovePermission(Guid permissionId)
    {
        var permission = _permissions.SingleOrDefault(p => p.Id == permissionId)
                         ?? throw new DomainException("Permission not found.");

        _permissions.Remove(permission);
        Touch();

        RaiseDomainEvent(new RolePermissionRemoved(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, permissionId));
    }

    public bool HasPermission(string resource, PermissionType permissionType) =>
        _permissions.Any(p => p.Resource == resource && p.PermissionType == permissionType);

    // ── Helpers ───────────────────────────────────────────
    private void Touch() => UpdatedAt = DateTime.UtcNow;
}