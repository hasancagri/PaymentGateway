namespace PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.ReadModels;

public class BankRouteSummary
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public Guid BankId { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal BankRate { get; set; }
    public decimal MerchantRate { get; set; }
}