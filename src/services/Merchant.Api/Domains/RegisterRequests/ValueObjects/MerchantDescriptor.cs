using System.Text.RegularExpressions;

namespace Merchant.Api.Domains.RegisterRequests.ValueObjects;

/// <summary>
/// Aday sitenin <c>/.well-known/merchant-descriptor.json</c> beyanının doğrulanmış kopyası (VO —
/// kimliği yok, private ctor + statik <see cref="Create"/>). Gateway'in zorunlu okuduğu alanlar:
/// legalName, taxId, contactEmail, webhookUrl (contracts/merchant-descriptor.md). contactEmail geçerli
/// e-posta, webhookUrl mutlak HTTPS olmalı; aksi halde başvuru talep oluşturmadan reddedilir (FR-002).
/// </summary>
public sealed class MerchantDescriptor
{
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private MerchantDescriptor()
    {
    }

    public string SchemaVersion { get; private set; } = string.Empty;
    public string Domain { get; private set; } = string.Empty;
    public string LegalName { get; private set; } = string.Empty;
    public string TaxId { get; private set; } = string.Empty;
    public string ContactEmail { get; private set; } = string.Empty;
    public string WebhookUrl { get; private set; } = string.Empty;
    public string? A2aCardUrl { get; private set; }

    public static ResultDomain<MerchantDescriptor> Create(
        string? schemaVersion,
        string? domain,
        string? legalName,
        string? taxId,
        string? contactEmail,
        string? webhookUrl,
        string? a2aCardUrl)
    {
        if (string.IsNullOrWhiteSpace(legalName))
            return ResultDomain<MerchantDescriptor>.Error(Required(nameof(LegalName)));
        if (string.IsNullOrWhiteSpace(taxId))
            return ResultDomain<MerchantDescriptor>.Error(Required(nameof(TaxId)));
        if (string.IsNullOrWhiteSpace(contactEmail) || !EmailRegex.IsMatch(contactEmail))
            return ResultDomain<MerchantDescriptor>.Error(InvalidFormat(nameof(ContactEmail)));
        if (!IsHttpsUrl(webhookUrl))
            return ResultDomain<MerchantDescriptor>.Error(InvalidFormat(nameof(WebhookUrl)));

        return ResultDomain<MerchantDescriptor>.Ok(new MerchantDescriptor
        {
            SchemaVersion = schemaVersion?.Trim() ?? string.Empty,
            Domain = domain?.Trim().ToLowerInvariant() ?? string.Empty,
            LegalName = legalName.Trim(),
            TaxId = taxId.Trim(),
            ContactEmail = contactEmail.Trim(),
            WebhookUrl = webhookUrl!.Trim(),
            A2aCardUrl = string.IsNullOrWhiteSpace(a2aCardUrl) ? null : a2aCardUrl.Trim()
        });
    }

    private static bool IsHttpsUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) &&
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private static MessageItem Required(string property) => new()
    {
        Property = property,
        Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
    };

    private static MessageItem InvalidFormat(string property) => new()
    {
        Property = property,
        Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT
    };
}