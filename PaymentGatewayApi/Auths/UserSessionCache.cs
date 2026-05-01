using PaymentGatewayApi.Modules.IAM.Roles.Enums;

namespace PaymentGatewayApi.Auths;

public record UserSessionCache
{
    public Guid UserId { get; init; }
    public List<PermissionCache> Permissions { get; init; } = [];
}

public record PermissionCache
{
    public required string Resource { get; init; }
    public PermissionType PermissionType { get; init; }
}