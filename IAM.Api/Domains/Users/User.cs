namespace IAM.Api.Domains.Users;

public sealed class User : AggregateRoot
{
    public Email Email { get; private set; } = null!;
    public FullName FullName { get; private set; } = null!;
    public PasswordHash Password { get; private set; } = null!;
    public UserStatus Status { get; private set; }
    public Guid? MerchantId { get; private set; }

    [Newtonsoft.Json.JsonProperty]
    private List<UserRole> _roles = [];
    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();

    private User() { }

    public static ResultDomain<User> Create(
        string email, string password, string firstName, string lastName, Guid? merchantId = null)
    {
        var emailResult = Email.Create(email);
        var passwordResult = PasswordHash.Create(password);
        var nameResult = FullName.Create(firstName, lastName);

        var errors = new List<MessageItem>();
        if (!emailResult.IsSuccess) errors.AddRange(emailResult.Messages!);
        if (!passwordResult.IsSuccess) errors.AddRange(passwordResult.Messages!);
        if (!nameResult.IsSuccess) errors.AddRange(nameResult.Messages!);
        if (errors.Count > 0) return ResultDomain<User>.Error(errors);

        return ResultDomain<User>.Ok(new User
        {
            Email = emailResult.Data!,
            Password = passwordResult.Data!,
            FullName = nameResult.Data!,
            Status = UserStatus.Active,
            MerchantId = merchantId,
        });
    }

    public bool Login(string plainPassword)
    {
        if (Status != UserStatus.Active) return false;
        if (!Password.Verify(plainPassword)) return false;

        return true;
    }

    public ResultDomain ChangePassword(string newPlainPassword)
    {
        var result = PasswordHash.Create(newPlainPassword);
        if (!result.IsSuccess) return ResultDomain.Error(result.Messages!);
        Password = result.Data!;
        return ResultDomain.Ok();
    }

    public void Activate() => Status = UserStatus.Active;

    public ResultDomain Deactivate()
    {
        if (Status == UserStatus.Passive)
            return ResultDomain.Error(new MessageItem { Code = "User.AlreadyPassive" });
        Status = UserStatus.Passive;
        return ResultDomain.Ok();
    }

    public ResultDomain AssignRole(Guid roleId)
    {
        if (_roles.Any(r => r.RoleId == roleId))
            return ResultDomain.Error(new MessageItem { Code = "User.RoleAlreadyAssigned" });
        _roles.Add(UserRole.Create(roleId));
        return ResultDomain.Ok();
    }

    public ResultDomain RemoveRole(Guid roleId)
    {
        var role = _roles.SingleOrDefault(r => r.RoleId == roleId);
        if (role is null)
            return ResultDomain.Error(new MessageItem { Code = "User.RoleNotAssigned" });
        _roles.Remove(role);
        return ResultDomain.Ok();
    }

    public ResultDomain AssignMerchant(Guid merchantId)
    {
        if (MerchantId is not null)
            return ResultDomain.Error(new MessageItem { Code = "User.AlreadyAssignedToMerchant" });
        MerchantId = merchantId;
        return ResultDomain.Ok();
    }

    public ResultDomain RemoveFromMerchant()
    {
        if (MerchantId is null)
            return ResultDomain.Error(new MessageItem { Code = "User.NotAssignedToMerchant" });
        MerchantId = null;
        return ResultDomain.Ok();
    }
}