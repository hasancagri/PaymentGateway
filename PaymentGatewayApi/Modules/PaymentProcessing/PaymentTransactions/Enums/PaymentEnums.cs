namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Enums;

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