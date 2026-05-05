using PaymentGatewayApi.Modules.BankIntegration.Banks.Enums;
using PaymentGatewayApi.Modules.BankIntegration.Banks.ValueObjects;

namespace PaymentGatewayApi.Modules.BankIntegration.Banks;

public sealed class Bank : AggregateRoot
{
    public BankName Name { get; private set; }
    public BankPriority Priority { get; private set; }
    public BankStatus Status { get; private set; }
    public BankApiUrl ApiUrl { get; private set; }

    private readonly List<string> _supportedCurrencies = [];
    public IReadOnlyCollection<string> SupportedCurrencies => _supportedCurrencies.AsReadOnly();

    private Bank()
    {
    }

    public static ResultDomain<Bank> Configure(string name, int priority, string apiUrl)
    {
        var nameResult = BankName.Create(name);
        var priorityResult = BankPriority.Create(priority);
        var apiUrlResult = BankApiUrl.Create(apiUrl);

        var errors = new List<MessageItem>();
        if (!nameResult.IsSuccess) errors.AddRange(nameResult.Messages!);
        if (!priorityResult.IsSuccess) errors.AddRange(priorityResult.Messages!);
        if (!apiUrlResult.IsSuccess) errors.AddRange(apiUrlResult.Messages!);
        if (errors.Count > 0) return ResultDomain<Bank>.Error(errors);

        return ResultDomain<Bank>.Ok(new Bank
        {
            Name = nameResult.Data!,
            Priority = priorityResult.Data!,
            ApiUrl = apiUrlResult.Data!,
            Status = BankStatus.Active,
        });
    }

    public ResultDomain Update(string name, int priority, string apiUrl)
    {
        var nameResult = BankName.Create(name);
        var priorityResult = BankPriority.Create(priority);
        var apiUrlResult = BankApiUrl.Create(apiUrl);

        var errors = new List<MessageItem>();
        if (!nameResult.IsSuccess) errors.AddRange(nameResult.Messages!);
        if (!priorityResult.IsSuccess) errors.AddRange(priorityResult.Messages!);
        if (!apiUrlResult.IsSuccess) errors.AddRange(apiUrlResult.Messages!);
        if (errors.Count > 0) return ResultDomain.Error(errors);

        Name = nameResult.Data!;
        Priority = priorityResult.Data!;
        ApiUrl = apiUrlResult.Data!;
        return ResultDomain.Ok();
    }

    public void Activate() => Status = BankStatus.Active;
    public void Deactivate() => Status = BankStatus.Passive;

    public ResultDomain AddSupportedCurrency(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3)
            return ResultDomain.Error(new MessageItem { Code = "Bank.InvalidCurrencyCode" });

        var code = currencyCode.ToUpperInvariant();
        if (_supportedCurrencies.Contains(code))
            return ResultDomain.Error(new MessageItem { Code = "Bank.CurrencyAlreadySupported", Params = [code] });

        _supportedCurrencies.Add(code);
        return ResultDomain.Ok();
    }

    public bool SupportsCurrency(string currencyCode) =>
        _supportedCurrencies.Contains(currencyCode.ToUpperInvariant());

    public bool IsAvailable() => Status == BankStatus.Active;
}