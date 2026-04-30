using Common.Shared;
using PaymentGatewayApi.Modules.MerchantManagement.Merchants.Entities;
using PaymentGatewayApi.Modules.MerchantManagement.Merchants.Enums;
using PaymentGatewayApi.Modules.MerchantManagement.Merchants.Events;
using PaymentGatewayApi.Modules.MerchantManagement.Merchants.ValueObjects;
using PaymentGatewayApi.Shared;

namespace PaymentGatewayApi.Modules.MerchantManagement.Merchants;

public sealed class Merchant : AggregateRoot
{
    // ── Identity ──────────────────────────────────────────
    public MerchantId Id { get; private set; }
    public MerchantName Name { get; private set; }
    public MerchantStatus Status { get; private set; }
    public ContactInfo ContactInfo { get; private set; }
    public MerchantAddress Address { get; private set; }
    public Mcc Mcc { get; private set; }

    // ── Collections ───────────────────────────────────────
    private readonly List<ApiKey> _apiKeys = [];
    private readonly List<MerchantBankAccount> _bankAccounts = [];
    private readonly List<MerchantCurrency> _currencies = [];
    private readonly List<IpAddress> _ipWhitelist = [];

    public IReadOnlyCollection<ApiKey> ApiKeys => _apiKeys.AsReadOnly();
    public IReadOnlyCollection<MerchantBankAccount> BankAccounts => _bankAccounts.AsReadOnly();
    public IReadOnlyCollection<MerchantCurrency> Currencies => _currencies.AsReadOnly();
    public IReadOnlyCollection<IpAddress> IpWhitelist => _ipWhitelist.AsReadOnly();

    // ── Audit ─────────────────────────────────────────────
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private const int MaxActiveApiKeys = 2;

    private Merchant()
    {
    } // EF Core

    // ── Factory ───────────────────────────────────────────
    public static Merchant Create(
        MerchantName name,
        ContactInfo contactInfo,
        MerchantAddress address,
        Mcc mcc)
    {
        var merchant = new Merchant
        {
            Id = MerchantId.New(),
            Name = name,
            Status = MerchantStatus.Active,
            ContactInfo = contactInfo,
            Address = address,
            Mcc = mcc,
            CreatedAt = DateTime.UtcNow
        };

        merchant.RaiseDomainEvent(new MerchantCreated(
            Guid.NewGuid(), DateTime.UtcNow,
            merchant.Id.Value,
            merchant.Name.Value,
            merchant.ContactInfo.Email,
            merchant.Address.Country));

        return merchant;
    }

    // ── Update ────────────────────────────────────────────
    public void Update(
        MerchantName name,
        ContactInfo contactInfo,
        MerchantAddress address,
        Mcc mcc)
    {
        Name = name;
        ContactInfo = contactInfo;
        Address = address;
        Mcc = mcc;
        Touch();

        RaiseDomainEvent(new MerchantUpdated(
            Guid.NewGuid(), DateTime.UtcNow, Id.Value));
    }

    // ── Status ────────────────────────────────────────────
    public void Activate(string reason)
    {
        if (Status == MerchantStatus.Active)
            throw new DomainException("Merchant is already active.");

        var old = Status;
        Status = MerchantStatus.Active;
        Touch();

        RaiseDomainEvent(new MerchantStatusChanged(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, old.ToString(), Status.ToString(), reason));
    }

    public void Deactivate(string reason)
    {
        if (Status == MerchantStatus.Passive)
            throw new DomainException("Merchant is already passive.");

        var old = Status;
        Status = MerchantStatus.Passive;
        Touch();

        RaiseDomainEvent(new MerchantStatusChanged(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, old.ToString(), Status.ToString(), reason));
    }

    public void Suspend(string reason)
    {
        if (Status == MerchantStatus.Suspended)
            throw new DomainException("Merchant is already suspended.");

        var old = Status;
        Status = MerchantStatus.Suspended;
        Touch();

        RaiseDomainEvent(new MerchantStatusChanged(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, old.ToString(), Status.ToString(), reason));
    }

    // ── API Key ───────────────────────────────────────────
    public ApiKeyValue GenerateApiKey()
    {
        if (_apiKeys.Count(k => k.IsActive()) >= MaxActiveApiKeys)
            throw new DomainException(
                $"A merchant can have at most {MaxActiveApiKeys} active API keys. " +
                "Revoke an existing key before generating a new one.");

        var keyValue = ApiKeyValue.Generate();
        var apiKey = ApiKey.Create(keyValue);
        _apiKeys.Add(apiKey);
        Touch();

        RaiseDomainEvent(new ApiKeyGenerated(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, apiKey.Id));

        return keyValue; // Plain text sadece bir kez döner
    }

    public void RevokeApiKey(Guid apiKeyId)
    {
        var key = _apiKeys.SingleOrDefault(k => k.Id == apiKeyId)
                  ?? throw new DomainException($"API key '{apiKeyId}' not found.");

        key.Revoke();
        Touch();

        RaiseDomainEvent(new ApiKeyRevoked(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, apiKeyId));
    }

    public bool VerifyApiKey(string candidate) =>
        _apiKeys.Any(k => k.IsActive() && k.KeyValue.Verify(candidate));

    // ── Bank Account ──────────────────────────────────────
    public void AddBankAccount(
        string iban,
        string swiftCode,
        string bankName,
        Currency currency,
        BankAccountType type)
    {
        if (_bankAccounts.Any(b => b.Iban == iban.Trim().ToUpperInvariant() && b.IsActive))
            throw new DomainException("A bank account with this IBAN already exists.");

        var account = MerchantBankAccount.Create(iban, swiftCode, bankName, currency, type);
        _bankAccounts.Add(account);
        Touch();

        RaiseDomainEvent(new BankAccountAdded(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, account.Id, account.Iban, currency.Code));
    }

    public void RemoveBankAccount(Guid bankAccountId)
    {
        var account = _bankAccounts.SingleOrDefault(b => b.Id == bankAccountId)
                      ?? throw new DomainException("Bank account not found.");

        account.Deactivate();
        Touch();

        RaiseDomainEvent(new BankAccountRemoved(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, bankAccountId));
    }

    // ── Currency ──────────────────────────────────────────
    public void AddCurrency(Currency currency)
    {
        if (_currencies.Any(c => c.Currency == currency))
            throw new DomainException($"Currency '{currency.Code}' is already supported.");

        _currencies.Add(MerchantCurrency.Create(currency));
        Touch();

        RaiseDomainEvent(new CurrencyAdded(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, currency.Code));
    }

    public void RemoveCurrency(Currency currency)
    {
        var entry = _currencies.SingleOrDefault(c => c.Currency == currency)
                    ?? throw new DomainException($"Currency '{currency.Code}' is not supported.");

        if (_bankAccounts.Any(b => b.IsActive && b.Currency == currency))
            throw new DomainException(
                $"Cannot remove '{currency.Code}': an active bank account uses this currency.");

        _currencies.Remove(entry);
        Touch();

        RaiseDomainEvent(new CurrencyRemoved(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, currency.Code));
    }

    // ── IP Whitelist ──────────────────────────────────────
    public void AddIpToWhitelist(IpAddress ip)
    {
        if (_ipWhitelist.Contains(ip))
            throw new DomainException($"IP '{ip.Value}' is already whitelisted.");

        _ipWhitelist.Add(ip);
        Touch();
    }

    public void RemoveIpFromWhitelist(IpAddress ip)
    {
        if (!_ipWhitelist.Remove(ip))
            throw new DomainException($"IP '{ip.Value}' is not in the whitelist.");

        Touch();
    }

    public bool IsIpAllowed(IpAddress ip) =>
        !_ipWhitelist.Any() || _ipWhitelist.Contains(ip);

    // ── Helpers ───────────────────────────────────────────
    private void Touch() => UpdatedAt = DateTime.UtcNow;
}