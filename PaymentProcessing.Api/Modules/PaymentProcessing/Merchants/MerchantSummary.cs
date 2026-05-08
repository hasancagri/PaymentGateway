namespace PaymentProcessing.Api.Modules.PaymentProcessing.Merchants;

// Stub — Task 8 will define the full MerchantSummary read model
public class MerchantSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? WebhookUrl { get; set; }
    public string Status { get; set; } = null!;
    public List<MerchantApiKey> ApiKeys { get; set; } = new();
}

public class MerchantApiKey
{
    public string KeyHash { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime? ExpiresAt { get; set; }
}