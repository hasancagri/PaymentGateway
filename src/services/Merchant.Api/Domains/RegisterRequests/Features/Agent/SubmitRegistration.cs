using System.Text.Json;
using Common.Mail;

namespace Merchant.Api.Domains.RegisterRequests.Features.Agent;

/// <summary>
/// US1 — merchant adayı başvurusu. Aday, gateway'e sabit bir <b>descriptor linki</b> verir; gateway
/// o linki okur (descriptor doğrula), alan adı sahipliğini domain-control challenge ile doğrular ve
/// geçerse <see cref="RegisterRequest"/>(Pending) oluşturup admin'e bildirim maili atar. Merchant
/// OLUŞMAZ (onayla doğar). Mükerrer koruma (FR-020): aynı domain için Pending/Approved talep varsa yeni açılmaz.
/// </summary>
public static class SubmitRegistration
{
    public record SubmitRegistrationCommand(string DescriptorUrl, string? ExternalRef = null);

    public class SubmitRegistrationResponse
    {
        /// <summary>"Pending" (talep oluştu) veya "ChallengeRequired" (aday değeri yayınlamalı).</summary>
        public string Status { get; set; } = string.Empty;
        public Guid? RequestId { get; set; }
        public string? Token { get; set; }
        public string? ExpectedValue { get; set; }
        public string? PublishPath { get; set; }
        public string? Message { get; set; }
    }

    [Transactional]
    public class SubmitRegistrationCommandHandler
    {
        // Dev: aday site (ECommerce) https self-signed dev cert kullanır → sertifikayı doğrulama
        // (yalnız descriptor/challenge okuması; prod'da gerçek cert). Timeout kısa.
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

            // 2) Mükerrer koruma (FR-020): aynı domain için Pending veya Approved talep varsa yeni açılmaz.
            var duplicate = await session.Query<RegisterRequest>()
                .Where(r => r.Domain == domain &&
                            (r.Status == RegisterRequestStatus.Pending || r.Status == RegisterRequestStatus.Approved))
                .AnyAsync(ct);
            if (duplicate)
                return FeatureObjectResultModel<SubmitRegistrationResponse>.Error(new MessageItem
                {
                    Property = nameof(domain),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_DUPLICATE
                });

            // 3) Challenge: bu domain için geçerli (Issued, süresi geçmemiş) bilet yoksa üret.
            var challenge = await session.Query<DomainControlChallenge>()
                .Where(c => c.Domain == domain && c.Status == ChallengeStatus.Issued)
                .OrderByDescending(c => c.CreatedTime)
                .FirstOrDefaultAsync(ct);

            if (challenge is null || challenge.ExpiresAtUtc <= DateTime.UtcNow)
            {
                challenge = DomainControlChallenge.Issue(domain);
                session.Store(challenge);
            }

            // 4) Adayın yayınladığı değeri aynı origin'deki challenge yolundan çek + doğrula.
            var challengeUrl = $"{descriptorUri.Scheme}://{descriptorUri.Authority}" +
                               $"/.well-known/merchant-challenge/{challenge.Token}";
            var fetched = await FetchChallengeValueAsync(challengeUrl, ct);
            var outcome = challenge.Verify(fetched, DateTime.UtcNow);
            session.Store(challenge); // Store = upsert (yeni bilet Update'te NonExistentDocument verirdi)

            if (outcome != ChallengeOutcome.Passed)
            {
                // Aday henüz yayınlamadı → değeri döndür, yayınlayınca tekrar çağırsın (FR-003).
                return FeatureObjectResultModel<SubmitRegistrationResponse>.Ok(new SubmitRegistrationResponse
                {
                    Status = "ChallengeRequired",
                    Token = challenge.Token,
                    ExpectedValue = challenge.ExpectedValue,
                    PublishPath = $"/.well-known/merchant-challenge/{challenge.Token}",
                    Message = outcome == ChallengeOutcome.Expired
                        ? "Bilet süresi doldu; yeni değeri yayınlayıp tekrar başvurun."
                        : "Belirtilen yola beklenen değeri yayınlayıp başvuruyu tekrarlayın."
                });
            }

            // 5) Kanıt geçti → başvuru oluştur (Pending) + admin bildirim maili (deterministik).
            var created = RegisterRequest.Create(domain, descriptor, outcome, cmd.ExternalRef);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<SubmitRegistrationResponse>.Error(created.Messages);

            session.Store(created.Data!);

            await NotifyAdminAsync(session, mail, config, logger, domain, created.Data!.Id, ct);

            return FeatureObjectResultModel<SubmitRegistrationResponse>.Ok(new SubmitRegistrationResponse
            {
                Status = "Pending",
                RequestId = created.Data!.Id,
                Message = "Başvuru alındı; admin onayı bekleniyor."
            });
        }

        // --- Admin "yeni başvuru" bildirim maili (FR-005/019) — OnboardingNotification kaydına yansır.
        private static async Task NotifyAdminAsync(
            IDocumentSession session, IMailSender mail, IConfiguration config,
            ILogger logger, string domain, Guid requestId, CancellationToken ct)
        {
            var adminEmail = config["Onboarding:AdminNotificationEmail"] ?? "admin@dropshop.local";
            var subject = $"Yeni merchant başvurusu: {domain}";
            var body = $"'{domain}' alan adı için yeni bir kayıt başvurusu alındı (talep {requestId}). " +
                       "Admin panelinden inceleyip onaylayın/reddedin.";

            var notification = OnboardingNotification.Create(NotificationKind.AdminNewRequest, adminEmail, subject);

            var send = await mail.SendAsync(adminEmail, subject, body, ct: ct);
            if (send.IsSuccess)
                notification.MarkSent();
            else
            {
                notification.MarkFailed("Mail.Mcp send_email başarısız");
                logger.LogWarning("Admin bildirim maili gönderilemedi: {Domain}", domain);
            }

            session.Store(notification);
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

        private static async Task<string?> FetchChallengeValueAsync(string url, CancellationToken ct)
        {
            try
            {
                using var resp = await Http.GetAsync(url, ct);
                return resp.IsSuccessStatusCode ? (await resp.Content.ReadAsStringAsync(ct)).Trim() : null;
            }
            catch
            {
                return null;
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