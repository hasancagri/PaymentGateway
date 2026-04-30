using PaymentGatewayApi.Modules.BankIntegration.Banks.Enums;
using PaymentGatewayApi.Modules.BankIntegration.Banks.Events;
using PaymentGatewayApi.Modules.BankIntegration.Banks.ValueObjects;

namespace PaymentGatewayApi.Modules.BankIntegration.Banks;

public sealed class Bank : AggregateRoot
{
    // ── Identity ──────────────────────────────────────────
    public BankId Id { get; private set; }
    public BankName Name { get; private set; }
    public BankPriority Priority { get; private set; }
    public BankStatus Status { get; private set; }

    // ── Collections ───────────────────────────────────────
    private readonly List<string> _supportedCurrencies = []; // ISO 4217 codes
    public IReadOnlyCollection<string> SupportedCurrencies => _supportedCurrencies.AsReadOnly();

    // ── Audit ─────────────────────────────────────────────
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Bank()
    {
    } // EF Core

    // ── Factory ───────────────────────────────────────────
    public static Bank Configure(BankName name, BankPriority priority)
    {
        var bank = new Bank
        {
            Id = BankId.New(),
            Name = name,
            Priority = priority,
            Status = BankStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        bank.RaiseDomainEvent(new BankConfigured(
            Guid.NewGuid(), DateTime.UtcNow,
            bank.Id.Value, bank.Name.Value));

        return bank;
    }

    // ── Update ────────────────────────────────────────────
    public void Update(BankName name, BankPriority priority)
    {
        Name = name;
        Priority = priority;
        Touch();

        RaiseDomainEvent(new BankUpdated(
            Guid.NewGuid(), DateTime.UtcNow, Id.Value));
    }

    // ── Status ────────────────────────────────────────────
    public void Activate()
    {
        var old = Status;
        Status = BankStatus.Active;
        Touch();

        RaiseDomainEvent(new BankStatusChanged(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, old.ToString(), Status.ToString()));
    }

    public void Deactivate()
    {
        var old = Status;
        Status = BankStatus.Passive;
        Touch();

        RaiseDomainEvent(new BankStatusChanged(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, old.ToString(), Status.ToString()));
    }

    // ── Currencies ────────────────────────────────────────
    public void AddSupportedCurrency(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3)
            throw new DomainException("Currency code must be 3 letters.");

        var code = currencyCode.ToUpperInvariant();

        if (_supportedCurrencies.Contains(code))
            throw new DomainException($"Currency '{code}' is already supported by this bank.");

        _supportedCurrencies.Add(code);
        Touch();
    }

    public bool SupportsCurrency(string currencyCode) =>
        _supportedCurrencies.Contains(currencyCode.ToUpperInvariant());

    public bool IsAvailable() => Status == BankStatus.Active;

    // ── Helpers ───────────────────────────────────────────
    private void Touch() => UpdatedAt = DateTime.UtcNow;
}