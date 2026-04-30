using PaymentGatewayApi.Modules.MerchantManagement.Merchants.Enums;
using PaymentGatewayApi.Modules.MerchantManagement.Merchants.ValueObjects;

namespace PaymentGatewayApi.Modules.MerchantManagement.Merchants.Entities;

public sealed class ApiKey
{
    public Guid Id { get; private set; }
    public ApiKeyValue KeyValue { get; private set; }
    public ApiKeyStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    private ApiKey()
    {
    } // EF Core

    internal static ApiKey Create(ApiKeyValue keyValue, TimeSpan? gracePeriod = null) => new()
    {
        Id = Guid.NewGuid(),
        KeyValue = keyValue,
        Status = ApiKeyStatus.Active,
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = gracePeriod.HasValue ? DateTime.UtcNow.Add(gracePeriod.Value) : null
    };

    internal void Revoke()
    {
        if (Status == ApiKeyStatus.Revoked)
            throw new DomainException("API key is already revoked.");

        Status = ApiKeyStatus.Revoked;
        RevokedAt = DateTime.UtcNow;
    }

    internal void Expire() => Status = ApiKeyStatus.Expired;

    public bool IsActive() =>
        Status == ApiKeyStatus.Active &&
        (ExpiresAt is null || ExpiresAt > DateTime.UtcNow);
}

public sealed class MerchantBankAccount
{
    public Guid Id { get; private set; }
    public string Iban { get; private set; }
    public string SwiftCode { get; private set; }
    public string BankName { get; private set; }
    public Currency Currency { get; private set; }
    public BankAccountType Type { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private MerchantBankAccount()
    {
    } // EF Core

    internal static MerchantBankAccount Create(
        string iban,
        string swiftCode,
        string bankName,
        Currency currency,
        BankAccountType type)
    {
        if (string.IsNullOrWhiteSpace(iban))
            throw new DomainException("IBAN cannot be empty.");
        if (string.IsNullOrWhiteSpace(swiftCode))
            throw new DomainException("SWIFT code cannot be empty.");

        return new MerchantBankAccount
        {
            Id = Guid.NewGuid(),
            Iban = iban.Trim().ToUpperInvariant(),
            SwiftCode = swiftCode.Trim().ToUpperInvariant(),
            BankName = bankName.Trim(),
            Currency = currency,
            Type = type,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    internal void Deactivate() => IsActive = false;
}

public sealed class MerchantCurrency
{
    public Guid Id { get; private set; }
    public Currency Currency { get; private set; }
    public DateTime AddedAt { get; private set; }

    private MerchantCurrency()
    {
    }

    internal static MerchantCurrency Create(Currency currency) => new()
    {
        Id = Guid.NewGuid(),
        Currency = currency,
        AddedAt = DateTime.UtcNow
    };
}