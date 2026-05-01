using PaymentGatewayApi.Modules.MerchantManagement.Merchants.Enums;
using PaymentGatewayApi.Modules.MerchantManagement.Merchants.ValueObjects;

namespace PaymentGatewayApi.Modules.MerchantManagement.Merchants.Entities;

public sealed class ApiKey : BaseModel
{
    public ApiKeyValue KeyValue { get; private set; }
    public ApiKeyStatus Status { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    private ApiKey()
    {
    }

    internal static ApiKey Create(ApiKeyValue keyValue, TimeSpan? gracePeriod = null) => new()
    {
        KeyValue = keyValue,
        Status = ApiKeyStatus.Active,
        ExpiresAt = gracePeriod.HasValue ? DateTime.UtcNow.Add(gracePeriod.Value) : null
    };

    internal ResultDomain Revoke()
    {
        if (Status == ApiKeyStatus.Revoked)
            return ResultDomain.Error(new MessageItem { Code = "ApiKey.AlreadyRevoked" });
        Status = ApiKeyStatus.Revoked;
        RevokedAt = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    internal void Expire() => Status = ApiKeyStatus.Expired;

    public bool IsActive() =>
        Status == ApiKeyStatus.Active &&
        (ExpiresAt is null || ExpiresAt > DateTime.UtcNow);
}

public sealed class MerchantBankAccount : BaseModel
{
    public string Iban { get; private set; }
    public string SwiftCode { get; private set; }
    public string BankName { get; private set; }
    public Currency Currency { get; private set; }
    public BankAccountType Type { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private MerchantBankAccount()
    {
    }

    internal static ResultDomain<MerchantBankAccount> Create(
        string iban, string swiftCode, string bankName, Currency currency, BankAccountType type)
    {
        var errors = new List<MessageItem>();
        if (string.IsNullOrWhiteSpace(iban))
            errors.Add(new MessageItem { Code = "MerchantBankAccount.IbanEmpty" });
        if (string.IsNullOrWhiteSpace(swiftCode))
            errors.Add(new MessageItem { Code = "MerchantBankAccount.SwiftEmpty" });
        if (errors.Count > 0) return ResultDomain<MerchantBankAccount>.Error(errors);

        return ResultDomain<MerchantBankAccount>.Ok(new MerchantBankAccount
        {
            Id = Guid.NewGuid(),
            Iban = iban.Trim().ToUpperInvariant(),
            SwiftCode = swiftCode.Trim().ToUpperInvariant(),
            BankName = bankName.Trim(),
            Currency = currency,
            Type = type,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
    }

    internal void Deactivate() => IsActive = false;
}

public sealed class MerchantCurrency : BaseModel
{
    public Currency Currency { get; private set; }
    public DateTime AddedAt { get; private set; }

    private MerchantCurrency()
    {
    }

    internal static MerchantCurrency Create(Currency currency) => new()
    {
        Currency = currency,
        AddedAt = DateTime.UtcNow
    };
}