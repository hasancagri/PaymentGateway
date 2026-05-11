namespace PaymentProcessing.Api.PaymentProcessing.Merchants;

public class MerchantSummary
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? WebhookUrl { get; private set; }
    public string Status { get; private set; } = "Active";

    [Newtonsoft.Json.JsonConstructor]
    private MerchantSummary() { }

    public static MerchantSummary Create(Guid id, string name, string? webhookUrl) => new()
    {
        Id = id,
        Name = name,
        WebhookUrl = webhookUrl,
        Status = "Active"
    };

    public void Update(string? name, string? webhookUrl)
    {
        if (name is not null) Name = name;
        if (webhookUrl is not null) WebhookUrl = webhookUrl;
    }

    public void UpdateStatus(string status) => Status = status;
}