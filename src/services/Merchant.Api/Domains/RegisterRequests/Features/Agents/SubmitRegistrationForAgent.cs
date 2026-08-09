
namespace Merchant.Api.Domains.RegisterRequests.Features.Agents;

/// <summary>
/// US1 — merchant adayı başvurusu. Aday, başvuru alanlarını (legalName/taxId/contactEmail/webhookUrl/
/// domain) doğrudan gönderir (push-inline, 016 — descriptor URL çekme YOK); alanlar doğrulanıp başvuru
/// doğrudan <see cref="RegisterRequestStatus.Pending"/> statüsünde oluşturulur; merchant'ın verdiği maile
/// "başvurun alındı" bildirimi gider. Domain-control challenge KALDIRILDI — sahiplik/uygunluk denetimi
/// admin'in insan incelemesidir. Merchant OLUŞMAZ (onayla doğar). Mükerrer koruma (FR-020): aynı domain
/// için Pending/Approved talep varsa yeni açılmaz.
/// </summary>
public static class SubmitRegistrationForAgent
{
    public record SubmitRegistrationCommand(
        string Domain,
        string LegalName,
        string TaxId,
        string ContactEmail,
        string WebhookUrl,
        string? MerchantMail = null);

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
        public async Task<FeatureObjectResultModel<SubmitRegistrationResponse>> Handle(
            SubmitRegistrationCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var domain = cmd.Domain?.Trim().ToLowerInvariant() ?? string.Empty;

            // 1) Aynı domain için aktif talep (Pending/Approved) varsa mükerrer RET (FR-020).
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

            // 2) Talep doğrudan Pending doğar (alan doğrulaması CreatePending'de); admin onayı bekler.
            var created = RegisterRequest.CreatePending(
                cmd.Domain, cmd.LegalName, cmd.TaxId, cmd.ContactEmail, cmd.WebhookUrl, cmd.MerchantMail);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<SubmitRegistrationResponse>.Error(created.Messages);

            var request = created.Data!;
            session.Store(request);

            // 3) Merchant maili verdiyse "başvurun alındı" bildirimini yayınla → Mail.Worker (SMTP).
            //    [Transactional] outbox: publish yalnız DB commit'te gider; mail yoksa atlanır (best-effort).
            if (!string.IsNullOrWhiteSpace(cmd.MerchantMail))
                await bus.PublishAsync(new Shared.IntegrationEvents.SendEmailRequested(
                    cmd.MerchantMail,
                    $"Başvurunuz alındı: {domain}",
                    $"'{domain}' alan adı için kayıt başvurunuz alındı (talep {request.Id}). " +
                    "Admin incelemesi tamamlanınca sonuç bu adrese bildirilecektir."));

            return FeatureObjectResultModel<SubmitRegistrationResponse>.Ok(new SubmitRegistrationResponse
            {
                Status = "Pending",
                RequestId = request.Id,
                Message = "Başvuru alındı; admin onayı bekleniyor."
            });
        }
    }
}
