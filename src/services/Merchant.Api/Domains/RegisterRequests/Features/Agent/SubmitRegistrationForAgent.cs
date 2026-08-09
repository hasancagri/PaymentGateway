using System.Text.Json;
using Common.Mail;

namespace Merchant.Api.Domains.RegisterRequests.Features.Agent;

/// <summary>
/// US1 — merchant adayı başvurusu. Aday, gateway'e sabit bir <b>descriptor linki</b> verir; gateway
/// o linki okur (descriptor doğrula) ve başvuruyu doğrudan <see cref="RegisterRequestStatus.Pending"/>
/// statüsünde oluşturur; admin'e bildirim maili gider. Domain-control challenge KALDIRILDI — sahiplik/
/// uygunluk denetimi admin'in insan incelemesidir (descriptor'daki legalName/taxId/contactEmail).
/// Merchant OLUŞMAZ (onayla doğar). Mükerrer koruma (FR-020): aynı domain için Pending/Approved talep
/// varsa yeni açılmaz.
/// </summary>
public static class SubmitRegistrationForAgent
{
    public record SubmitRegistrationCommand(string DescriptorUrl, string? ExternalRef = null);

    public class SubmitRegistrationResponse
    {
        /// <summary>Başarıda "Pending" (talep admin onayı bekler).</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>ECommerce "benim sürecim" korelasyon referansı.</summary>
        public Guid? RequestId { get; set; }
        public string? Message { get; set; }
    }

    [Transactional]
    public class SubmitRegistrationCommandHandler
    {
        // Dev: aday site (ECommerce) https self-signed dev cert kullanır → sertifikayı doğrulama
        // (yalnız descriptor okuması; prod'da gerçek cert). Timeout kısa.
        private static readonly HttpClient Http = new(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        })
        { Timeout = TimeSpan.FromSeconds(10) };

        public async Task<FeatureObjectResultModel<SubmitRegistrationResponse>> Handle(
            SubmitRegistrationCommand cmd,
            IDocumentSession session,
            IMailSender mail,
            IConfiguration config,
            ILogger<SubmitRegistrationCommandHandler> logger,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.DescriptorUrl) ||
                !Uri.TryCreate(cmd.DescriptorUrl, UriKind.Absolute, out var descriptorUri))
                return FeatureObjectResultModel<SubmitRegistrationResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.DescriptorUrl),
                    Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT
                });

            // 1) Descriptor'ı çek + doğrula (FR-002). Erişilemez/eksik → talep yok.
            var descriptorResult = await FetchDescriptorAsync(cmd.DescriptorUrl, ct);
            if (!descriptorResult.IsSuccess)
                return FeatureObjectResultModel<SubmitRegistrationResponse>.Error(descriptorResult.Messages);

            var descriptor = descriptorResult.Data!;
            var domain = string.IsNullOrWhiteSpace(descriptor.Domain)
                ? descriptorUri.Authority.ToLowerInvariant()
                : descriptor.Domain;

            // 2) Aynı domain için aktif talep (Pending/Approved) varsa mükerrer RET (FR-020).
            //    Rejected talep yeniden başvuruya engel değildir.
            var duplicate = await session.Query<RegisterRequest>()
                .Where(r => r.Domain == domain &&
                            (r.Status == RegisterRequestStatus.Pending ||
                             r.Status == RegisterRequestStatus.Approved))
                .AnyAsync(ct);

            if (duplicate)
                return FeatureObjectResultModel<SubmitRegistrationResponse>.Error(new MessageItem
                {
                    Property = nameof(domain),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_DUPLICATE
                });

            // 3) Talep doğrudan Pending doğar; admin onayı bekler.
            var created = RegisterRequest.CreatePending(domain, descriptor, cmd.ExternalRef);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<SubmitRegistrationResponse>.Error(created.Messages);

            var request = created.Data!;
            session.Store(request);

            // 4) Admin'e "yeni başvuru" bildirim maili.
            await NotifyAdminAsync(mail, config, logger, domain, request.Id, ct);

            return FeatureObjectResultModel<SubmitRegistrationResponse>.Ok(new SubmitRegistrationResponse
            {
                Status = "Pending",
                RequestId = request.Id,
                Message = "Başvuru alındı; admin onayı bekleniyor."
            });
        }

        // --- Admin "yeni başvuru" bildirim maili (FR-005). Mail best-effort; başarısızlık akışı kesmez.
        private static async Task NotifyAdminAsync(
            IMailSender mail, IConfiguration config,
            ILogger logger, string domain, Guid requestId, CancellationToken ct)
        {
            var adminEmail = config["Onboarding:AdminNotificationEmail"] ?? "admin@dropshop.local";
            var subject = $"Yeni merchant başvurusu: {domain}";
            var body = $"'{domain}' alan adı için yeni bir kayıt başvurusu alındı (talep {requestId}). " +
                       "Admin panelinden inceleyip onaylayın/reddedin.";

            var send = await mail.SendAsync(adminEmail, subject, body, ct: ct);
            if (!send.IsSuccess)
                logger.LogWarning("Admin bildirim maili gönderilemedi: {Domain}", domain);
        }

        private static async Task<ResultDomain<MerchantDescriptor>> FetchDescriptorAsync(
            string url, CancellationToken ct)
        {
            try
            {
                using var resp = await Http.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode)
                    return DescriptorError();

                var json = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string? Str(string name) =>
                    root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
                        ? el.GetString()
                        : null;

                string? a2a = null;
                if (root.TryGetProperty("agent", out var agent) && agent.ValueKind == JsonValueKind.Object &&
                    agent.TryGetProperty("a2aCardUrl", out var a2aEl) && a2aEl.ValueKind == JsonValueKind.String)
                    a2a = a2aEl.GetString();

                return MerchantDescriptor.Create(Str("schemaVersion"), Str("domain"), Str("legalName"),
                    Str("taxId"), Str("contactEmail"), Str("webhookUrl"), a2a);
            }
            catch
            {
                return DescriptorError();
            }
        }

        private static ResultDomain<MerchantDescriptor> DescriptorError() =>
            ResultDomain<MerchantDescriptor>.Error(new MessageItem
            {
                Property = "Descriptor",
                Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
            });
    }
}