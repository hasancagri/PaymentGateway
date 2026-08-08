namespace Merchant.Api.Domains.RegisterRequests;

/// <summary>
/// Deterministik mail gönderim kaydı (FR-019). Mail çökse akış sessizce "başarılı" saymaz; admin
/// Failed kayıtları görür/retry edebilir. Yalnız deterministik mailler (admin bildirim + aktivasyon);
/// komisyon Excel maili agentik/harici LLM → domain kaydı tutmaz.
/// </summary>
public class OnboardingNotification : AggregateRoot
{
    private OnboardingNotification()
    {
    }

    public Guid? MerchantId { get; private set; }
    public NotificationKind Kind { get; private set; }
    public string Recipient { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public NotificationStatus Status { get; private set; } = NotificationStatus.Pending;
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }

    public static OnboardingNotification Create(
        NotificationKind kind, string recipient, string subject, Guid? merchantId = null) =>
        new()
        {
            Kind = kind,
            Recipient = recipient,
            Subject = subject,
            MerchantId = merchantId,
            Status = NotificationStatus.Pending
        };

    public void MarkSent()
    {
        Attempts++;
        Status = NotificationStatus.Sent;
        LastError = null;
        UpdatedTime = DateTime.UtcNow;
    }

    public void MarkFailed(string error)
    {
        Attempts++;
        Status = NotificationStatus.Failed;
        LastError = error;
        UpdatedTime = DateTime.UtcNow;
    }
}

public enum NotificationKind
{
    AdminNewRequest = 1,
    Activation = 2
}

public enum NotificationStatus
{
    Pending = 1,
    Sent = 2,
    Failed = 3
}