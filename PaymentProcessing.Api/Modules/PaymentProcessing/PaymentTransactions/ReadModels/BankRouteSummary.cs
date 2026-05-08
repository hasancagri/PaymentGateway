namespace PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.ReadModels;

// Stub — Task 8 will define the full BankRouteSummary read model
public class BankRouteSummary
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public Guid BankId { get; set; }
    public string BankName { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public decimal BankRate { get; set; }
    public decimal MerchantRate { get; set; }
    public bool IsActive { get; set; }
}