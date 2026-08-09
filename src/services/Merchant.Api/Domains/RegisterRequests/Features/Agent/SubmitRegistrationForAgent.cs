using System.Text.Json;
using Common.Mail;

namespace Merchant.Api.Domains.RegisterRequests.Features.Agent;

/// <summary>
/// US1 — merchant adayı başvurusu. Aday, gateway'e sabit bir <b>descriptor linki</b> verir; gateway
/// o linki okur (descriptor doğrula), alan adı sahipliğini <b>domain-control challenge</b> ile doğrular.
/// 015: challenge artık ayrı aggregate değil, <see cref="RegisterRequest"/>'in alanıdır — talep challenge
/// geçmeden ÖNCE <see cref="RegisterRequestStatus.AwaitingDomainControl"/> statüsünde doğar, kanıt geçince
/// aynı talep <see cref="RegisterRequestStatus.Pending"/>'e ilerler ve admin'e bildirim maili gider.
/// Merchant OLUŞMAZ (onayla doğar). Mükerrer koruma (FR-020): aynı domain için Pending/Approved talep
/// varsa yeni açılmaz; AwaitingDomainControl varsa YENİDEN KULLANILIR (yeni talep açılmaz).
/// </summary>
public static class SubmitRegistrationForAgent
{
    public record SubmitRegistrationCommand(string DescriptorUrl, string? ExternalRef = null);

    public class SubmitRegistrationResponse
    {
        /// <summary>"Pending" (talep Pending'e geçti) veya "ChallengeRequired" (aday değeri yayınlamalı).</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>015: artık ChallengeRequired'da da dolu — ECommerce "benim sürecim" korelasyon referansı.</summary>
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

            // 2) Aynı domain için aktif talep (015): Pending/Approved varsa mükerrer RET (FR-020);
            //    AwaitingDomainControl varsa yeniden kullan; yoksa yeni AwaitingDomainControl talebi doğur.
            var existing = await session.Query<RegisterRequest>()
                .Where(r => r.Domain == domain &&
                            (r.Status == RegisterRequestStatus.AwaitingDomainControl ||
                             r.Status == RegisterRequestStatus.Pending ||
                             r.Status == RegisterRequestStatus.Approved))
                .OrderByDescending(r => r.CreatedTime)
                .FirstOrDefaultAsync(ct);

            if (existing is not null && existing.Status != RegisterRequestStatus.AwaitingDomainControl)
                return FeatureObjectResultModel<SubmitRegistrationResponse>.Error(new MessageItem
                {
                    Property = nameof(domain),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_DUPLICATE
                });

            RegisterRequest request;
            if (existing is not null)
            {
                request = existing;
            }
            else
            {
                var created = RegisterRequest.CreateAwaiting(domain, descriptor, cmd.ExternalRef);
                if (!created.IsSuccess)
                    return FeatureObjectResultModel<SubmitRegistrationResponse>.Error(created.Messages);
                request = created.Data!;
            }

            // 3) Challenge bileti süresi dolmuşsa (ve henüz geçmemişse) aynı talep üzerinde yenile.
            if (request.ChallengeResult != ChallengeOutcome.Passed && DateTime.UtcNow > request.ChallengeExpiresAtUtc)
                request.IssueChallenge(DateTime.UtcNow);

            // 4) Adayın yayınladığı değeri aynı origin'deki challenge yolundan çek + doğrula.
            var challengeUrl = $"{descriptorUri.Scheme}://{descriptorUri.Authority}" +
                               $"/.well-known/merchant-challenge/{request.ChallengeToken}";
            var fetched = await FetchChallengeValueAsync(challengeUrl, ct);
            var verifyResult = request.VerifyChallenge(fetched, DateTime.UtcNow);
            var outcome = verifyResult.Data!;

            session.Store(request); // Store = upsert (yeni talep Update'te NonExistentDocument verirdi)

            if (outcome != ChallengeOutcome.Passed)
            {
                // Aday henüz yayınlamadı → değeri döndür, yayınlayınca tekrar çağırsın (FR-003).
                // 015: talep artık kalıcı → RequestId de döner (ECommerce "sürecim ne oldu?" ile takip eder).
                return FeatureObjectResultModel<SubmitRegistrationResponse>.Ok(new SubmitRegistrationResponse
                {
                    Status = "ChallengeRequired",
                    RequestId = request.Id,
                    Token = request.ChallengeToken,
                    ExpectedValue = request.ChallengeExpectedValue,
                    PublishPath = $"/.well-known/merchant-challenge/{request.ChallengeToken}",
                    Message = outcome == ChallengeOutcome.Expired
                        ? "Bilet süresi doldu; yeni değeri yayınlayıp tekrar başvurun."
                        : "Belirtilen yola beklenen değeri yayınlayıp başvuruyu tekrarlayın."
                });
            }

            // 5) Kanıt geçti → talep artık Pending (VerifyChallenge içinde geçti) + admin bildirim maili.
            await NotifyAdminAsync(mail, config, logger, domain, request.Id, ct);

            return FeatureObjectResultModel<SubmitRegistrationResponse>.Ok(new SubmitRegistrationResponse
            {
                Status = "Pending",
                RequestId = request.Id,
                Message = "Başvuru alındı; admin onayı bekleniyor."
            });
        }

        // --- Admin "yeni başvuru" bildirim maili (FR-005). 015: ayrı durum kaydı (OnboardingNotification)
        // TUTULMAZ — mail best-effort, sonuç ILogger ile loglanır; başarısızlık akışı kesmez.
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