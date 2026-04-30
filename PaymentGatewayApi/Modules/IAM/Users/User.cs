using PaymentGatewayApi.Modules.IAM.Roles.ValueObjects;
using PaymentGatewayApi.Modules.IAM.Users.Entities;
using PaymentGatewayApi.Modules.IAM.Users.Enums;
using PaymentGatewayApi.Modules.IAM.Users.Events;
using PaymentGatewayApi.Modules.IAM.Users.ValueObjects;

namespace PaymentGatewayApi.Modules.IAM.Users;

public sealed class User : AggregateRoot
{
    // ── Identity ──────────────────────────────────────────
    public UserId Id { get; private set; }
    public Email Email { get; private set; }
    public PasswordHash Password { get; private set; }
    public FullName FullName { get; private set; }
    public Language Language { get; private set; }
    public UserStatus Status { get; private set; }
    public Guid? MerchantId { get; private set; } // Cross-BC reference (read-only)

    // ── Collections ───────────────────────────────────────
    private readonly List<UserRole> _roles = [];
    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();

    // ── Login tracking ────────────────────────────────────
    public int FailedLoginCount { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public DateTime? LockedUntil { get; private set; }

    // ── Audit ─────────────────────────────────────────────
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private const int MaxFailedAttempts = 5;

    private User()
    {
    } // EF Core

    // ── Factory ───────────────────────────────────────────
    public static User Create(
        Email email,
        PasswordHash password,
        FullName fullName,
        Language language,
        Guid? merchantId = null)
    {
        var user = new User
        {
            Id = UserId.New(),
            Email = email,
            Password = password,
            FullName = fullName,
            Language = language,
            Status = UserStatus.Active,
            MerchantId = merchantId,
            CreatedAt = DateTime.UtcNow
        };

        user.RaiseDomainEvent(new UserCreated(
            Guid.NewGuid(), DateTime.UtcNow,
            user.Id.Value, user.Email.Value, user.FullName.Display));

        return user;
    }

    // ── Authentication ────────────────────────────────────
    public bool Login(string plainPassword, string ipAddress)
    {
        if (Status != UserStatus.Active)
            throw new DomainException("User account is not active.");

        if (LockedUntil.HasValue && LockedUntil > DateTime.UtcNow)
            throw new DomainException("Account is temporarily locked. Try again later.");

        if (!Password.Verify(plainPassword))
        {
            FailedLoginCount++;

            RaiseDomainEvent(new UserLoginFailed(
                Guid.NewGuid(), DateTime.UtcNow,
                Email.Value, ipAddress, FailedLoginCount));

            if (FailedLoginCount >= MaxFailedAttempts)
            {
                Status = UserStatus.Locked;
                LockedUntil = DateTime.UtcNow.AddMinutes(30);
                Touch();

                RaiseDomainEvent(new UserStatusChanged(
                    Guid.NewGuid(), DateTime.UtcNow,
                    Id.Value, UserStatus.Active.ToString(), Status.ToString()));
            }

            return false;
        }

        FailedLoginCount = 0;
        LastLoginAt = DateTime.UtcNow;
        LockedUntil = null;
        Touch();

        RaiseDomainEvent(new UserLoggedIn(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, ipAddress));

        return true;
    }

    // ── Password ──────────────────────────────────────────
    public void ChangePassword(string newPlainPassword)
    {
        Password = PasswordHash.Create(newPlainPassword);
        Touch();

        RaiseDomainEvent(new UserPasswordChanged(
            Guid.NewGuid(), DateTime.UtcNow, Id.Value));
    }

    // ── Status ────────────────────────────────────────────
    public void Activate()
    {
        var old = Status;
        Status = UserStatus.Active;
        FailedLoginCount = 0;
        LockedUntil = null;
        Touch();

        RaiseDomainEvent(new UserStatusChanged(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, old.ToString(), Status.ToString()));
    }

    public void Deactivate()
    {
        if (Status == UserStatus.Passive)
            throw new DomainException("User is already passive.");

        var old = Status;
        Status = UserStatus.Passive;
        Touch();

        RaiseDomainEvent(new UserStatusChanged(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, old.ToString(), Status.ToString()));
    }

    // ── Roles ─────────────────────────────────────────────
    public void AssignRole(RoleId roleId)
    {
        if (_roles.Any(r => r.RoleId == roleId))
            throw new DomainException("Role is already assigned to this user.");

        _roles.Add(UserRole.Create(roleId));
        Touch();

        RaiseDomainEvent(new UserRoleAssigned(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, roleId.Value));
    }

    public void RevokeRole(RoleId roleId)
    {
        var role = _roles.SingleOrDefault(r => r.RoleId == roleId)
                   ?? throw new DomainException("Role is not assigned to this user.");

        _roles.Remove(role);
        Touch();

        RaiseDomainEvent(new UserRoleRevoked(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, roleId.Value));
    }

    // ── Helpers ───────────────────────────────────────────
    private void Touch() => UpdatedAt = DateTime.UtcNow;
}