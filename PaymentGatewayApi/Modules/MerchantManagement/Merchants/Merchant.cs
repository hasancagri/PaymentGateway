using PaymentGatewayApi.Modules.MerchantManagement.Merchants.Entities;
using PaymentGatewayApi.Modules.MerchantManagement.Merchants.Enums;
using PaymentGatewayApi.Modules.MerchantManagement.Merchants.ValueObjects;

namespace PaymentGatewayApi.Modules.MerchantManagement.Merchants;

public sealed class Merchant : AggregateRoot
{
    private const int MaxActiveApiKeys = 2;
    public MerchantName Name { get; private set; }
    public MerchantStatus Status { get; private set; }
    public ContactInfo ContactInfo { get; private set; }
    public MerchantAddress Address { get; private set; }
    public Mcc Mcc { get; private set; }
    public WebhookUrl WebhookUrl { get; private set; }

    private readonly List<ApiKey> _apiKeys = [];
    private readonly List<MerchantBankAccount> _bankAccounts = [];

    public IReadOnlyCollection<ApiKey> ApiKeys => _apiKeys.AsReadOnly();
    public IReadOnlyCollection<MerchantBankAccount> BankAccounts => _bankAccounts.AsReadOnly();


    private Merchant()
    {
    }

    public static ResultDomain<Merchant> Create(
        string name, string email, string phone,
        string country, string city, string mcc, string webhookUrl)
    {
        var nameResult = MerchantName.Create(name);
        var contactResult = ContactInfo.Create(email, phone);
        var addressResult = MerchantAddress.Create(country, city);
        var mccResult = Mcc.Create(mcc);
        var webhookUrlResult = WebhookUrl.Create(webhookUrl);

        var errors = new List<MessageItem>();
        if (!nameResult.IsSuccess) errors.AddRange(nameResult.Messages!);
        if (!contactResult.IsSuccess) errors.AddRange(contactResult.Messages!);
        if (!addressResult.IsSuccess) errors.AddRange(addressResult.Messages!);
        if (!mccResult.IsSuccess) errors.AddRange(mccResult.Messages!);
        if (!webhookUrlResult.IsSuccess) errors.AddRange(webhookUrlResult.Messages!);
        if (errors.Count > 0) return ResultDomain<Merchant>.Error(errors);

        return ResultDomain<Merchant>.Ok(new Merchant
        {
            Name = nameResult.Data!,
            ContactInfo = contactResult.Data!,
            Address = addressResult.Data!,
            Mcc = mccResult.Data!,
            WebhookUrl = webhookUrlResult.Data!,
            Status = MerchantStatus.Active,
        });
    }

    public ResultDomain Update(
        string name, string email, string phone,
        string country, string city, string mcc, string webhookUrl)
    {
        var nameResult = MerchantName.Create(name);
        var contactResult = ContactInfo.Create(email, phone);
        var addressResult = MerchantAddress.Create(country, city);
        var mccResult = Mcc.Create(mcc);
        var webhookUrlResult = WebhookUrl.Create(webhookUrl);

        var errors = new List<MessageItem>();
        if (!nameResult.IsSuccess) errors.AddRange(nameResult.Messages!);
        if (!contactResult.IsSuccess) errors.AddRange(contactResult.Messages!);
        if (!addressResult.IsSuccess) errors.AddRange(addressResult.Messages!);
        if (!mccResult.IsSuccess) errors.AddRange(mccResult.Messages!);
        if (!webhookUrlResult.IsSuccess) errors.AddRange(webhookUrlResult.Messages!);
        if (errors.Count > 0) return ResultDomain.Error(errors);

        Name = nameResult.Data!;
        ContactInfo = contactResult.Data!;
        Address = addressResult.Data!;
        Mcc = mccResult.Data!;
        WebhookUrl = webhookUrlResult.Data!;
        return ResultDomain.Ok();
    }

    public ResultDomain Activate(string reason)
    {
        if (Status == MerchantStatus.Active)
            return ResultDomain.Error(new MessageItem { Code = "Merchant.AlreadyActive" });
        Status = MerchantStatus.Active;
        return ResultDomain.Ok();
    }

    public ResultDomain Deactivate(string reason)
    {
        if (Status == MerchantStatus.Passive)
            return ResultDomain.Error(new MessageItem { Code = "Merchant.AlreadyPassive" });
        Status = MerchantStatus.Passive;
        return ResultDomain.Ok();
    }

    public ResultDomain Suspend(string reason)
    {
        if (Status == MerchantStatus.Suspended)
            return ResultDomain.Error(new MessageItem { Code = "Merchant.AlreadySuspended" });
        Status = MerchantStatus.Suspended;
        return ResultDomain.Ok();
    }

    public ResultDomain<ApiKeyValue> GenerateApiKey()
    {
        if (_apiKeys.Count(k => k.IsActive()) >= MaxActiveApiKeys)
            return ResultDomain<ApiKeyValue>.Error(new MessageItem
            {
                Code = "Merchant.MaxApiKeysReached",
                Params = [MaxActiveApiKeys.ToString()]
            });

        var keyValue = ApiKeyValue.Generate();
        _apiKeys.Add(ApiKey.Create(keyValue));
        return ResultDomain<ApiKeyValue>.Ok(keyValue);
    }

    public ResultDomain RevokeApiKey(Guid apiKeyId)
    {
        var key = _apiKeys.SingleOrDefault(k => k.Id == apiKeyId);
        if (key is null)
            return ResultDomain.Error(new MessageItem { Code = "ApiKey.NotFound", Params = [apiKeyId.ToString()] });
        return key.Revoke();
    }
    
    public ResultDomain AddBankAccount(
        string iban, string swiftCode, string bankName, Currency currency)
    {
        if (_bankAccounts.Any(b => b.Iban == iban.Trim().ToUpperInvariant() && b.IsActive))
            return ResultDomain.Error(new MessageItem { Code = "MerchantBankAccount.IbanDuplicate" });

        var result = MerchantBankAccount.Create(iban, swiftCode, bankName, currency);
        if (!result.IsSuccess) return ResultDomain.Error(result.Messages!);

        _bankAccounts.Add(result.Data!);
        return ResultDomain.Ok();
    }

    public ResultDomain RemoveBankAccount(Guid bankAccountId)
    {
        var account = _bankAccounts.SingleOrDefault(b => b.Id == bankAccountId);
        if (account is null)
            return ResultDomain.Error(new MessageItem { Code = "MerchantBankAccount.NotFound" });
        account.Deactivate();
        return ResultDomain.Ok();
    }
}