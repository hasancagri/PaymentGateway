using Payment.Api.Domains.StoreOnboarding;
using Payment.Api.Domains.StoreOnboarding.BankOnboarding;

namespace Payment.Api.Domains.StoreOnboarding.Features.Commands;

/// <summary>
/// 091 US3 (FR-011): Reddedilen bir bankaya yeniden başvuru (store-api-key). İlgili banka başvurusu yoksa
/// BANK_NOT_FOUND; Rejected değilse ALREADY_SUBMITTED (yalnız red sonrası). Adapter'a yeni referral gönderir,
/// aggregate.Resubmit ile o bankayı tekrar Referred'a çeker. Diğer bankalar + store durumu etkilenmez (izolasyon).
/// </summary>
public static class ResubmitBankApplication
{
    public record Resubmit(string PgStoreRef, string BankCode);

    [Transactional]
    public class Handler
    {
        public async Task<FeatureResultModel> Handle(
            Resubmit msg, IDocumentSession session, IEnumerable<IBankOnboardingAdapter> adapters,
            StoreOnboardingOptions options, CancellationToken ct)
        {
            if (!Guid.TryParse(msg.PgStoreRef, out var id))
                return FeatureResultModel.Error(new MessageItem { Code = StoreOnboardingResourceConstants.ONBOARDING_NOT_FOUND });

            var onboarding = await session.LoadAsync<StoreOnboarding>(id, ct);
            if (onboarding is null || onboarding.Profile is null)
                return FeatureResultModel.Error(new MessageItem { Code = StoreOnboardingResourceConstants.ONBOARDING_NOT_FOUND });

            var adapter = adapters.FirstOrDefault(a => a.BankCode == msg.BankCode);
            if (adapter is null)
                return FeatureResultModel.Error(new MessageItem { Code = StoreOnboardingResourceConstants.BANK_NOT_FOUND });

            var callbackUrl = $"{options.CallbackBaseUrl}/internal/onboarding/callback/{onboarding.CallbackToken}";
            var referral = new OnboardingReferral(onboarding.Profile, msg.BankCode, callbackUrl, onboarding.CallbackToken);
            var result = await adapter.SubmitReferralAsync(referral, ct);
            var outcome = result.IsSuccess && result.Data is not null
                ? result.Data
                : OnboardingOutcome.Rejected(StoreOnboardingResourceConstants.BANK_FIELD_MISSING);

            var resubmitted = onboarding.Resubmit(msg.BankCode, outcome, DateTime.UtcNow);
            if (!resubmitted.IsSuccess)
                return FeatureResultModel.Error(resubmitted.Messages);

            session.Store(onboarding);
            return FeatureResultModel.Ok();
        }
    }
}
