using PaymentGatewayApi.Modules.IAM.Roles.ValueObjects;

namespace PaymentGatewayApi.Modules.IAM.Users.Entities;

public sealed class UserRole
{
    public Guid Id { get; private set; }
    public RoleId RoleId { get; private set; }
    public DateTime AssignedAt { get; private set; }

    private UserRole()
    {
    } // EF Core

    internal static UserRole Create(RoleId roleId) => new()
    {
        Id = Guid.NewGuid(),
        RoleId = roleId,
        AssignedAt = DateTime.UtcNow
    };
}