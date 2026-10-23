using Payment.Api.Domains.StoreOnboarding;
using Payment.Api.Domains.StoreOnboarding.BankOnboarding;
using Payment.Api.Domains.StoreOnboarding.Credentials;
using Payment.Api.Domains.StoreOnboarding.Features.Commands;

namespace Payment.Api.Domains.StoreOnboarding.Process;

/// <summary>
/// 091 US4 (FR-012): BC'nin KENDİ dayanıklı süreci (conventions `Process/`) — kaçan/geç banka sonucu
/// webhook'unu toparlayan watchdog (InDoubtReconciliation aynası). SubmitOnboardingProfile her Referred banka
/// için zamanlanmış Check yayınlar. Handler bankadaki başvuru durumunu sorar (QueryApplicationStatusAsync):
/// Approved → credential sakla + RecordBankOutcome (+ ilk onayda store Live bildir); Rejected → kaydet;
/// hâlâ "inceleniyor" (Submitted) → ERKEN TERMINAL VERMEZ, sınırlı yeniden zamanlanır (tükenirse bırakılır).
/// Otorite = bankanın kaydı. İlk onayda OnboardingResultDelivery (webhook ile aynı yol).
/// </summary>
public static class OnboardingReconciliation
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(10);

    public record Check(Guid StoreOnboardingId, string BankCode, string BankApplicationRef, int Attempt);

    [Transactional]
    public class CheckHandler
    {
        public async Task Handle(
            Check msg, IDocumentSession session, IEnumerable<IBankOnboardingAdapter> adapters,
            IBankCredentialStore credentialStore, IMessageBus bus, OnboardingStoreCallbackOptions storeCallback,
            CancellationToken ct)
        {
            var onboarding = await session.LoadAsync<StoreOnboarding>(msg.StoreOnboardingId, ct);
            if (onboarding is null) return;

            var app = onboarding.Applications.FirstOrDefault(a => a.BankCode == msg.BankCode);
            if (app is null || app.Status != BankApplicationStatus.Referred) return; // çözülmüş → iş yok

            var adapter = adapters.FirstOrDefault(a => a.BankCode == msg.BankCode);
            if (adapter is null) return;

            var status = await adapter.QueryApplicationStatusAsync(msg.BankApplicationRef, ct);
            var outcome = status.Data;
            if (outcome is null) return;

            // Hâlâ inceleniyor (Submitted) → erken terminal YOK; sınırlı yeniden dene (FR-012).
            if (!ReconcileDecision.ShouldApply(outcome))
            {
                if (msg.Attempt + 1 < MaxAttempts)
                    await bus.PublishAsync(msg with { Attempt = msg.Attempt + 1 },
                        new DeliveryOptions { ScheduleDelay = RetryDelay });
                return;
            }

            Guid? credentialRef = null;
            if (outcome.Kind == OnboardingOutcomeKind.Approved && outcome.Credential is not null)
                credentialRef = await credentialStore.SaveAsync(onboarding.MerchantId, msg.BankCode, outcome.Credential, ct);

            var now = DateTime.UtcNow;
            var wasLive = onboarding.IsLive;
            var recorded = onboarding.RecordBankOutcome(msg.BankCode, outcome, credentialRef, now);
            if (!recorded.IsSuccess) return;

            session.Store(onboarding);

            if (onboarding.IsLive && !wasLive)
                await bus.PublishAsync(new OnboardingResultDelivery.Deliver(
                    storeCallback.CallbackUrl, onboarding.Id.ToString(), onboarding.MerchantId.ToString(),
                    nameof(OnboardingStatus.Live), msg.BankCode, now));
        }
    }
}

/// <summary>091 US4: reconcile saf kararı (test-first, İlke VIII domain-TDD). Bankadan gelen sonuç terminal
/// (Approved/Rejected) ise uygula; hâlâ "inceleniyor" (Submitted) ise UYGULAMA (erken terminal yok, FR-012).</summary>
public static class ReconcileDecision
{
    public static bool ShouldApply(OnboardingOutcome outcome)
        => outcome.Kind is OnboardingOutcomeKind.Approved or OnboardingOutcomeKind.Rejected;
}
