namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.ValueObjects;

public sealed record OrderId
{
    public string Value { get; }
    private OrderId(string value) => Value = value;

    public static ResultDomain<OrderId> Create(string value)
    {
        var errors = new List<MessageItem>();
        if (string.IsNullOrWhiteSpace(value))
            errors.Add(new MessageItem { Code = "OrderId.Empty" });
        else if (value.Length > 64)
            errors.Add(new MessageItem { Code = "OrderId.TooLong" });
        if (errors.Count > 0) return ResultDomain<OrderId>.Error(errors);
        return ResultDomain<OrderId>.Ok(new OrderId(value.Trim()));
    }

    public static OrderId FromPersistence(string value) => new(value);
    public override string ToString() => Value;
}

public sealed record CardInfo
{
    public string EncryptedCardNumber { get; }
    public string CardHolderName      { get; }
    public string ExpiryMonth         { get; }
    public string ExpiryYear          { get; }
    public string CardHolderIp        { get; }

    [JsonConstructor]
    private CardInfo(
        string encryptedCardNumber, string cardHolderName,
        string expiryMonth, string expiryYear, string cardHolderIp)
    {
        EncryptedCardNumber = encryptedCardNumber;
        CardHolderName      = cardHolderName;
        ExpiryMonth         = expiryMonth;
        ExpiryYear          = expiryYear;
        CardHolderIp        = cardHolderIp;
    }

    public static ResultDomain<CardInfo> Create(
        string encryptedCardNumber, string cardHolderName,
        string expiryMonth, string expiryYear, string cardHolderIp)
    {
        var errors = new List<MessageItem>();
        if (string.IsNullOrWhiteSpace(encryptedCardNumber))
            errors.Add(new MessageItem { Code = "CardInfo.CardNumberEmpty" });
        if (string.IsNullOrWhiteSpace(cardHolderName))
            errors.Add(new MessageItem { Code = "CardInfo.CardHolderNameEmpty" });
        if (!System.Net.IPAddress.TryParse(cardHolderIp, out _))
            errors.Add(new MessageItem { Code = "CardInfo.InvalidIpAddress" });
        if (errors.Count > 0) return ResultDomain<CardInfo>.Error(errors);

        return ResultDomain<CardInfo>.Ok(new CardInfo(
            encryptedCardNumber,
            cardHolderName.Trim(),
            expiryMonth.Trim(),
            expiryYear.Trim(),
            cardHolderIp.Trim()));
    }
}

public sealed record CommissionInfo
{
    public decimal BankRate       { get; }
    public decimal MerchantRate   { get; }
    public decimal BankAmount     { get; }
    public decimal MerchantAmount { get; }
    public decimal NetAmount      { get; }

    [JsonConstructor]
    private CommissionInfo(
        decimal bankRate, decimal merchantRate,
        decimal bankAmount, decimal merchantAmount, decimal netAmount)
    {
        BankRate       = bankRate;
        MerchantRate   = merchantRate;
        BankAmount     = bankAmount;
        MerchantAmount = merchantAmount;
        NetAmount      = netAmount;
    }

    public static ResultDomain<CommissionInfo> Create(decimal grossAmount, decimal bankRate, decimal merchantRate)
    {
        var errors = new List<MessageItem>();
        if (bankRate < 0 || merchantRate < 0)
            errors.Add(new MessageItem { Code = "CommissionInfo.NegativeRates" });
        if (merchantRate < bankRate)
            errors.Add(new MessageItem { Code = "CommissionInfo.MerchantRateBelowBankRate" });
        if (errors.Count > 0) return ResultDomain<CommissionInfo>.Error(errors);

        var bankAmount     = Math.Round(grossAmount * bankRate / 100, 2);
        var merchantAmount = Math.Round(grossAmount * merchantRate / 100, 2);
        var netAmount      = Math.Round(grossAmount - merchantAmount, 2);
        return ResultDomain<CommissionInfo>.Ok(new CommissionInfo(bankRate, merchantRate, bankAmount, merchantAmount, netAmount));
    }
}

public sealed record BankRoutingInfo
{
    public Guid   SelectedBankId { get; }
    public string MerchantCode   { get; }
    public string TerminalCode   { get; }

    [JsonConstructor]
    private BankRoutingInfo(Guid selectedBankId, string merchantCode, string terminalCode)
    {
        SelectedBankId = selectedBankId;
        MerchantCode   = merchantCode;
        TerminalCode   = terminalCode;
    }

    public static ResultDomain<BankRoutingInfo> Create(Guid selectedBankId, string merchantCode, string terminalCode)
    {
        var errors = new List<MessageItem>();
        if (selectedBankId == Guid.Empty)
            errors.Add(new MessageItem { Code = "BankRoutingInfo.BankIdEmpty" });
        if (string.IsNullOrWhiteSpace(merchantCode))
            errors.Add(new MessageItem { Code = "BankRoutingInfo.MerchantCodeEmpty" });
        if (string.IsNullOrWhiteSpace(terminalCode))
            errors.Add(new MessageItem { Code = "BankRoutingInfo.TerminalCodeEmpty" });
        if (errors.Count > 0) return ResultDomain<BankRoutingInfo>.Error(errors);

        return ResultDomain<BankRoutingInfo>.Ok(
            new BankRoutingInfo(selectedBankId, merchantCode.Trim(), terminalCode.Trim()));
    }
}