namespace PaymentProcessing.Api.Modules.PaymentProcessing.Merchants;

public class MerchantSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? WebhookUrl { get; set; }
    public string Status { get; set; } = "Active";
}