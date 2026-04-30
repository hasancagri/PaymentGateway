namespace PaymentGatewayApi.Modules.IAM.Users.Events;

public sealed record UserCreated(
    Guid EventId,
    DateTime OccuredOn,
    Guid UserId,
    string Email,
    string FullName
) : IDomainEvent;

public sealed record UserStatusChanged(
    Guid EventId,
    DateTime OccuredOn,
    Guid UserId,
    string OldStatus,
    string NewStatus
) : IDomainEvent;

public sealed record UserPasswordChanged(
    Guid EventId,
    DateTime OccuredOn,
    Guid UserId
) : IDomainEvent;

public sealed record UserRoleAssigned(
    Guid EventId,
    DateTime OccuredOn,
    Guid UserId,
    Guid RoleId
) : IDomainEvent;

public sealed record UserRoleRevoked(
    Guid EventId,
    DateTime OccuredOn,
    Guid UserId,
    Guid RoleId
) : IDomainEvent;

public sealed record UserLoggedIn(
    Guid EventId,
    DateTime OccuredOn,
    Guid UserId,
    string IpAddress
) : IDomainEvent;

public sealed record UserLoginFailed(
    Guid EventId,
    DateTime OccuredOn,
    string Email,
    string IpAddress,
    int FailCount
) : IDomainEvent;