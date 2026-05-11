namespace IAM.Api.Domains.Users.Entities;

public sealed class UserRole : BaseModel
{
    public Guid RoleId { get; init; }
    public DateTime AssignedAt { get; init; }

    private UserRole()
    {
    }

    internal static UserRole Create(Guid roleId) => new()
    {
        RoleId = roleId,
        AssignedAt = DateTime.UtcNow
    };
}