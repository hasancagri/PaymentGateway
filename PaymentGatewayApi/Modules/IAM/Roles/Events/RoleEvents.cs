namespace PaymentGatewayApi.Modules.IAM.Roles.Events;

public sealed record RoleCreated(
    Guid EventId,
    DateTime OccurredOn,
    Guid RoleId,
    string RoleName
);

public sealed record RolePermissionAdded(
    Guid EventId,
    DateTime OccurredOn,
    Guid RoleId,
    string Resource,
    string PermissionType
);

public sealed record RolePermissionRemoved(
    Guid EventId,
    DateTime OccurredOn,
    Guid RoleId,
    Guid PermissionId
);