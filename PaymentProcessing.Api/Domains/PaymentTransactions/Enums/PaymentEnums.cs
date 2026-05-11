namespace PaymentProcessing.Api.PaymentProcessing.PaymentTransactions.Enums;

public enum TransactionStatus
{
    Pending  = 1,
    Approved = 2,
    Declined = 3,
    Failed   = 4
}

public enum TransactionType
{
    Auth = 1
}

// Moved from CommissionManagement.BankCommissions.Enums to keep PaymentProcessing self-contained
public enum CardBrand
{
    Visa = 1,
    Mastercard = 2,
    Amex = 3,
    Troy = 4
}

public enum CardType
{
    Consumer = 1,
    Commercial = 2
}

public enum TransactionRegion
{
    Domestic = 1,
    International = 2
}