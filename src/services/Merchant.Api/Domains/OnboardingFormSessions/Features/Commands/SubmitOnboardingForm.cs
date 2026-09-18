namespace Merchant.Api.Domains.OnboardingFormSessions.Features.Commands;

// 045 US1: hosted form POST'u — başvuru doğrulaması RegisterRequest.Submit'te (İlke II; formda
// kural yok). E-posta OTURUMDAN gelir (store'un verdiği başvuru kimliği; form alanı salt-görsel).
// Doğrulama hatası oturumu TÜKETMEZ; başarı tüketir + PG Admin bildirim maili (029 davranışı
// buraya taşındı — [Transactional] outbox). Mükerrer kuralları SubmitRegistration İKİZİ
// (bilinçli tekrar; US4 sökümünde tek kopya bu kalır).
public static class SubmitOnboardingForm
{
    public record SubmitOnboardingFormCommand(
        string Token,
        string Type,
        string Name,
        string GsmNumber,
        string Address,
        string Iban,
        string ContactName,
        string ContactSurname,
        string? IdentityNumber,
        string? TaxOffice,
        string? TaxNumber,
        string? LegalCompanyTitle);

    public class SubmitOnboardingFormResponse
    {
        public Guid RequestId { get; set; }
    }

    [Transactional]
    public class SubmitOnboardingFormCommandHandler
    {
        public async Task<FeatureObjectResultModel<SubmitOnboardingFormResponse>> Handle(
            SubmitOnboardingFormCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            Merchant.Api.Options.AdminNotification adminNotification,
            CancellationToken ct)
        {
            var now = DateTimeOffset.UtcNow;
            var formSession = await session.Query<OnboardingFormSession>()
                .FirstOrDefaultAsync(x => x.Token == cmd.Token, ct);

            // Bilinmeyen/tüketilmiş/süresi geçmiş token → nötr NotFound (token doğruluğu sızdırılmaz).
            if (formSession is null || !formSession.IsUsable(now))
                return FeatureObjectResultModel<SubmitOnboardingFormResponse>.NotFound();

            if (!Enum.TryParse<MerchantType>(cmd.Type?.Trim(), ignoreCase: true, out var type))
                return FeatureObjectResultModel<SubmitOnboardingFormResponse>.Error(new MessageItem
                { Property = "Type", Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE });

            // Mükerrer kuralı (oturum açıldıktan sonra başka kanaldan doğan başvuru yarışı dahil).
            var statuses = await session.Query<RegisterRequest>()
                .Where(r => r.Email.ToLower() == formSession.Email)
                .Select(r => r.Status)
                .ToListAsync(ct);
            if (statuses.Contains(RegisterRequestStatus.Pending))
                return FeatureObjectResultModel<SubmitOnboardingFormResponse>.Error(new MessageItem
                { Property = "Email", Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_DUPLICATE });
            if (statuses.Contains(RegisterRequestStatus.Approved))
                return FeatureObjectResultModel<SubmitOnboardingFormResponse>.Error(new MessageItem
                { Property = "Email", Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });

            var result = RegisterRequest.Submit(
                type, cmd.Name, formSession.Email, cmd.GsmNumber, cmd.Address, cmd.Iban,
                cmd.ContactName, cmd.ContactSurname,
                cmd.IdentityNumber, cmd.TaxOffice, cmd.TaxNumber, cmd.LegalCompanyTitle);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<SubmitOnboardingFormResponse>.Error(result.Messages);

            var consumed = formSession.Consume(now);
            if (!consumed.IsSuccess)
                return FeatureObjectResultModel<SubmitOnboardingFormResponse>.NotFound();
            session.Store(formSession);

            var request = result.Data!;
            session.Store(request);

            // PG Admin bildirimi (029 FR-011 davranışı) — outbox: yalnız DB commit'te gider.
            await bus.PublishAsync(new Shared.IntegrationEvents.SendEmailRequested(
                adminNotification.AdminEmail,
                "PG'ye kayıt yaptırmak isteyen var",
                $"{request.Name} ({request.Email}) hosted formdan PG'ye kayıt başvurusu yaptı " +
                $"(requestId: {request.Id}). admin_get_pending_registrations ile inceleyip " +
                "onaylayabilir/reddedebilirsiniz."));

            return FeatureObjectResultModel<SubmitOnboardingFormResponse>.Ok(
                new SubmitOnboardingFormResponse { RequestId = request.Id });
        }
    }
}
