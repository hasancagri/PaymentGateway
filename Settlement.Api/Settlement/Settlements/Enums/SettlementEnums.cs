namespace PaymentGatewayApi.Modules.Settlement.Settlements.Enums;

public enum SettlementStatus
{
    Pending    = 1,
    Processing = 2,
    Completed  = 3,
    Failed     = 4
}

public enum BalanceMovementType
{
    Credit     = 1,
    Debit      = 2,
    Adjustment = 3
}

public enum WithdrawalStatus
{
    Requested = 1,
    Approved  = 2,
    Rejected  = 3,
    Processed = 4
}
