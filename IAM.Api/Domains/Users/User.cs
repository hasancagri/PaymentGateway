using IAM.Api.Domains.Users.Entities;
using IAM.Api.Domains.Users.Enums;
using IAM.Api.Domains.Users.ValueObjects;

namespace PaymentGatewayApi.Modules.IAM.Users;

public sealed class User : AggregateRoot
{
    public Email Email { get; init; }
    public FullName FullName { get; init; }

    private PasswordHash _password = null!;
    public PasswordHash Password { get => _password; init => _password = value; }

    private UserStatus _status;
    public UserStatus Status { get => _status; init => _status = value; }

    private Guid? _merchantId;
    public Guid? MerchantId { get => _merchantId; init => _merchantId = value; }

    [Newtonsoft.Json.JsonProperty]
    private List<UserRole> _roles = [];
    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();

    private DateTime? _lastLoginAt;
    public DateTime? LastLoginAt { get => _lastLoginAt; init => _lastLoginAt = value; }

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

        _lastLoginAt = DateTime.UtcNow;
        return true;
    }

    public ResultDomain ChangePassword(string newPlainPassword)
    {
        var result = PasswordHash.Create(newPlainPassword);
        if (!result.IsSuccess) return ResultDomain.Error(result.Messages!);
        _password = result.Data!;
        return ResultDomain.Ok();
    }

    public void Activate() => _status = UserStatus.Active;

    public ResultDomain Deactivate()
    {
        if (_status == UserStatus.Passive)
            return ResultDomain.Error(new MessageItem { Code = "User.AlreadyPassive" });
        _status = UserStatus.Passive;
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
        _merchantId = merchantId;
        return ResultDomain.Ok();
    }

    public ResultDomain RemoveFromMerchant()
    {
        if (_merchantId is null)
            return ResultDomain.Error(new MessageItem { Code = "User.NotAssignedToMerchant" });
        _merchantId = null;
        return ResultDomain.Ok();
    }
}