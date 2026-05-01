namespace PaymentGatewayApi.Modules.IAM.Users.Entities;

public sealed class UserRole : BaseModel
{
    public Guid RoleId { get; private set; }
    public DateTime AssignedAt { get; private set; }

    private UserRole()
    {
    }

    internal static UserRole Create(Guid roleId) => new()
    {
        RoleId = roleId,
        AssignedAt = DateTime.UtcNow
    };
}