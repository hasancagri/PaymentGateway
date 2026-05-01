namespace PaymentGatewayApi.Modules.IAM.Users.Events;

public sealed record UserCreated(
    Guid EventId,
    DateTime OccurredOn,
    Guid UserId,
    string Email,
    string FullName
);

public sealed record UserStatusChanged(
    Guid EventId,
    DateTime OccurredOn,
    Guid UserId,
    string OldStatus,
    string NewStatus
);

public sealed record UserPasswordChanged(
    Guid EventId,
    DateTime OccurredOn,
    Guid UserId
);

public sealed record UserRoleAssigned(
    Guid EventId,
    DateTime OccurredOn,
    Guid UserId,
    Guid RoleId
);

public sealed record UserRoleRemoved(
    Guid EventId,
    DateTime OccurredOn,
    Guid UserId,
    Guid RoleId
);

public sealed record UserLoggedIn(
    Guid EventId,
    DateTime OccurredOn,
    Guid UserId,
    string IpAddress
);

public sealed record UserLoginFailed(
    Guid EventId,
    DateTime OccurredOn,
    string Email,
    string IpAddress,
    int FailCount
);