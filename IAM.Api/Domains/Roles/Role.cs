namespace IAM.Api.Domains.Roles;

public sealed class Role : AggregateRoot
{
    public RoleName Name { get; init; }
    public bool IsSystem { get; init; }

    private Role()
    {
    }

    public static ResultDomain<Role> Create(string name, bool isSystem = false)
    {
        var nameResult = RoleName.Create(name);
        if (!nameResult.IsSuccess) return ResultDomain<Role>.Error(nameResult.Messages!);

        return ResultDomain<Role>.Ok(new Role { Name = nameResult.Data!, IsSystem = isSystem });
    }
}