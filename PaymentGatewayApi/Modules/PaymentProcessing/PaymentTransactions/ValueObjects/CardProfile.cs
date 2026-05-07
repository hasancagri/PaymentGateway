using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.Enums;

namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.ValueObjects;

public sealed record CardProfile
{
    public CardBrand CardBrand { get; init; }
    public CardType CardType { get; init; }
    public TransactionRegion TransactionRegion { get; init; }
    public string? IssuingMemberId { get; init; }
}