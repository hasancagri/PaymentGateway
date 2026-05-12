namespace IAM.Api.Domains.Roles;

public sealed class Role : AggregateRoot
{
    public RoleName Name { get; private set; }
    public bool IsSystem { get; private set; }

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