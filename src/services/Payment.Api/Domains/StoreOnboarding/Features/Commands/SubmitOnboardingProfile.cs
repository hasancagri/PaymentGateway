using Payment.Api.Domains.StoreOnboarding;
using Payment.Api.Domains.StoreOnboarding.BankOnboarding;
using Payment.Api.Domains.StoreOnboarding.Process;

namespace Payment.Api.Domains.StoreOnboarding.Features.Commands;

/// <summary>
/// 091 US1 (FR-004/FR-005/FR-006): Store hosted formu gönderir. Çekirdek profil doğrulanır (StoreProfile.Create;
/// eksik/IBAN/MCC → INVALID_PROFILE, bankalara İLETİLMEZ). Geçerliyse başvuru yapılandırılmış her bankaya
/// adapter wire ile fan-out edilir (banka başına BankApplication). Banka-özel zorunlu alan eksikse o banka
/// submit-öncesi Rejected (B kararı), diğerleri Referred. Banka farkı adapter'da — aggregate wire görmez (İlke VII).
/// </summary>
public static class SubmitOnboardingProfile
{
    public record SubmitOnboardingProfileCommand(
        string OpenToken, string LegalName, string TaxNumber, string Iban, string Mcc,
        string WebsiteUrl, string ContactName, string ContactEmail);

    public record BankStatusItem(string BankCode, string Status, string? ReasonCode);

    public class SubmitOnboardingProfileResponse
    {
        public string PgStoreRef { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<BankStatusItem> Banks { get; set; } = new();
    }

    [Transactional]
    public class SubmitOnboardingProfileCommandHandler
    {
        public async Task<FeatureObjectResultModel<SubmitOnboardingProfileResponse>> Handle(
            SubmitOnboardingProfileCommand cmd, IDocumentSession session,
            IEnumerable<IBankOnboardingAdapter> adapters, StoreOnboardingOptions options, IMessageBus bus,
            CancellationToken ct)
        {
            var onboarding = await session.Query<StoreOnboarding>()
                .Where(x => x.OpenToken == cmd.OpenToken)
                .FirstOrDefaultAsync(ct);

            if (onboarding is null)
                return FeatureObjectResultModel<SubmitOnboardingProfileResponse>.Error(new MessageItem
                { Code = StoreOnboardingResourceConstants.ONBOARDING_NOT_FOUND });

            var profile = StoreProfile.Create(
                cmd.LegalName, cmd.TaxNumber, cmd.Iban, cmd.Mcc, cmd.WebsiteUrl, cmd.ContactName, cmd.ContactEmail);
            if (!profile.IsSuccess)
                return FeatureObjectResultModel<SubmitOnboardingProfileResponse>.Error(profile.Messages);

            // Fan-out: her yapılandırılmış bankaya AYRI referral — PARALEL (Task.WhenAll; ayrı istekler aynı anda
            // gider, gerçek bankada latency toplanmaz). Sonuç BankCodes sırasında toplanır (deterministik).
            var callbackUrl = $"{options.CallbackBaseUrl}/internal/onboarding/callback/{onboarding.CallbackToken}";
            var referralTasks = options.BankCodes.Select(async bankCode =>
            {
                var adapter = adapters.FirstOrDefault(a => a.BankCode == bankCode);
                if (adapter is null)
                    return (BankCode: bankCode, Result: OnboardingOutcome.Rejected(StoreOnboardingResourceConstants.BANK_NOT_FOUND));

                var referral = new OnboardingReferral(profile.Data!, bankCode, callbackUrl, onboarding.CallbackToken);
                var result = await adapter.SubmitReferralAsync(referral, ct);
                return (BankCode: bankCode, Result: result.IsSuccess && result.Data is not null
                    ? result.Data
                    : OnboardingOutcome.Rejected(StoreOnboardingResourceConstants.BANK_FIELD_MISSING));
            });
            var fanout = (await Task.WhenAll(referralTasks))
                .Select(x => (x.BankCode, x.Result)).ToList();

            var submitted = onboarding.SubmitProfile(profile.Data!, fanout, DateTime.UtcNow);
            if (!submitted.IsSuccess)
                return FeatureObjectResultModel<SubmitOnboardingProfileResponse>.Error(submitted.Messages);

            session.Store(onboarding);

            // US4 watchdog: Referred her banka için zamanlanmış reconcile Check (kaçan webhook emniyeti, FR-012).
            foreach (var app in onboarding.Applications.Where(a => a.Status == BankApplicationStatus.Referred))
                await bus.PublishAsync(
                    new OnboardingReconciliation.Check(onboarding.Id, app.BankCode, app.BankApplicationRef ?? string.Empty, 0),
                    new DeliveryOptions { ScheduleDelay = TimeSpan.FromMinutes(10) });

            return FeatureObjectResultModel<SubmitOnboardingProfileResponse>.Ok(new SubmitOnboardingProfileResponse
            {
                PgStoreRef = onboarding.Id.ToString(),
                Status = onboarding.Status.ToString(),
                Banks = onboarding.Applications
                    .Select(a => new BankStatusItem(a.BankCode, a.Status.ToString(), a.ReasonCode)).ToList()
            });
        }
    }
}
