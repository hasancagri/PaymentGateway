namespace MerchantManagement.Api.Domains.Merchants.Entities;

public sealed class ApiKey : BaseModel
{
    public ApiKeyValue KeyValue { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public ApiKeyStatus Status { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    [Newtonsoft.Json.JsonConstructor]
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
    public string Iban { get; init; }
    public string SwiftCode { get; init; }
    public string BankName { get; init; }
    public Currency Currency { get; init; }
    public DateTime CreatedAt { get; init; }

    [Newtonsoft.Json.JsonConstructor]
    private MerchantBankAccount()
    {
    }

    internal static ResultDomain<MerchantBankAccount> Create(
        string iban, string swiftCode, string bankName, Currency currency)
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
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
    }

    internal void Deactivate() => IsActive = false;
}