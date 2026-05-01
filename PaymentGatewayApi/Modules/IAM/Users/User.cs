using PaymentGatewayApi.Modules.IAM.Users.Entities;
using PaymentGatewayApi.Modules.IAM.Users.Enums;
using PaymentGatewayApi.Modules.IAM.Users.ValueObjects;

namespace PaymentGatewayApi.Modules.IAM.Users;

public sealed class User : AggregateRoot
{
    public Email Email { get; private set; }
    public PasswordHash Password { get; private set; }
    public FullName FullName { get; private set; }
    public UserStatus Status { get; private set; }
    public Guid? MerchantId { get; private set; }

    private readonly List<UserRole> _roles = [];
    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();

    public DateTime? LastLoginAt { get; private set; }

    private User()
    {
    }

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

        LastLoginAt = DateTime.UtcNow;
        return true;
    }

    public ResultDomain ChangePassword(string newPlainPassword)
    {
        var result = PasswordHash.Create(newPlainPassword);
        if (!result.IsSuccess) return ResultDomain.Error(result.Messages!);
        Password = result.Data!;
        return ResultDomain.Ok();
    }

    public void Activate()
    {
        Status = UserStatus.Active;
    }

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
}