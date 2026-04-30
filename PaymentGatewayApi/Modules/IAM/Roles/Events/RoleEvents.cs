namespace PaymentGatewayApi.Modules.IAM.Roles.Events;

public sealed record RoleCreated(
    Guid     EventId,
    DateTime OccuredOn,
    Guid     RoleId,
    string   RoleName
) : IDomainEvent;

public sealed record RolePermissionAdded(
    Guid     EventId,
    DateTime OccuredOn,
    Guid     RoleId,
    string   Resource,
    string   PermissionType
) : IDomainEvent;

public sealed record RolePermissionRemoved(
    Guid     EventId,
    DateTime OccuredOn,
    Guid     RoleId,
    Guid     PermissionId
) : IDomainEvent;
