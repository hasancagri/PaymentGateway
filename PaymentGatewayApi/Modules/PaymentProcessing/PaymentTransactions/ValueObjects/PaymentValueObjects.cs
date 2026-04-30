namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.ValueObjects;

public sealed record TransactionId(Guid Value)
{
    public static TransactionId New()            => new(Guid.NewGuid());
    public static TransactionId From(Guid value) => new(value);
    public override string ToString()            => Value.ToString();
}

public sealed record OrderId
{
    public string Value { get; }

    public OrderId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Order ID cannot be empty.");
        if (value.Length > 64)
            throw new DomainException("Order ID cannot exceed 64 characters.");

        Value = value.Trim();
    }

    public override string ToString() => Value;
}


public sealed record CardInfo
{
    public string EncryptedCardNumber { get; } // AES encrypted — plain text never stored
    public string CardHolderName      { get; }
    public string ExpiryMonth         { get; }
    public string ExpiryYear          { get; }
    public string CardHolderIp        { get; }

    public CardInfo(
        string encryptedCardNumber,
        string cardHolderName,
        string expiryMonth,
        string expiryYear,
        string cardHolderIp)
    {
        if (string.IsNullOrWhiteSpace(encryptedCardNumber))
            throw new DomainException("Card number cannot be empty.");
        if (string.IsNullOrWhiteSpace(cardHolderName))
            throw new DomainException("Card holder name cannot be empty.");
        if (!System.Net.IPAddress.TryParse(cardHolderIp, out _))
            throw new DomainException("Invalid card holder IP address.");

        EncryptedCardNumber = encryptedCardNumber;
        CardHolderName      = cardHolderName.Trim();
        ExpiryMonth         = expiryMonth.Trim();
        ExpiryYear          = expiryYear.Trim();
        CardHolderIp        = cardHolderIp.Trim();
    }
}


public sealed record CommissionInfo
{
    public decimal BankRate       { get; }
    public decimal MerchantRate   { get; }
    public decimal BankAmount     { get; }
    public decimal MerchantAmount { get; }
    public decimal NetAmount      { get; }

    public CommissionInfo(
        decimal grossAmount,
        decimal bankRate,
        decimal merchantRate)
    {
        if (bankRate < 0 || merchantRate < 0)
            throw new DomainException("Commission rates cannot be negative.");
        if (merchantRate < bankRate)
            throw new DomainException("Merchant rate cannot be less than bank rate.");

        BankRate       = bankRate;
        MerchantRate   = merchantRate;
        BankAmount     = Math.Round(grossAmount * bankRate     / 100, 2);
        MerchantAmount = Math.Round(grossAmount * merchantRate / 100, 2);
        NetAmount      = Math.Round(grossAmount - MerchantAmount, 2);
    }
}

public sealed record BankRoutingInfo
{
    public Guid   SelectedBankId { get; }
    public string MerchantCode   { get; }
    public string TerminalCode   { get; }

    public BankRoutingInfo(Guid selectedBankId, string merchantCode, string terminalCode)
    {
        if (selectedBankId == Guid.Empty)
            throw new DomainException("Selected bank ID cannot be empty.");
        if (string.IsNullOrWhiteSpace(merchantCode))
            throw new DomainException("Merchant code cannot be empty.");
        if (string.IsNullOrWhiteSpace(terminalCode))
            throw new DomainException("Terminal code cannot be empty.");

        SelectedBankId = selectedBankId;
        MerchantCode   = merchantCode.Trim();
        TerminalCode   = terminalCode.Trim();
    }
}
