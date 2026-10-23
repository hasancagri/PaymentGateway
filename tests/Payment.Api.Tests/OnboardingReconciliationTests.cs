using Payment.Api.Domains.StoreOnboarding.BankOnboarding;
using Payment.Api.Domains.StoreOnboarding.Process;
using Payment.Api.Domains.StoreOnboarding.ValueObjects;

namespace Payment.Api.Tests;

// 091 US4 (İlke VIII Domain-TDD): reconcile saf kararı — terminal (Approved/Rejected) uygula; hâlâ
// "inceleniyor" (Submitted) → UYGULAMA (erken terminal yok, FR-012).
public class OnboardingReconciliationTests
{
    [Fact]
    public void ShouldApply_Approved_True()
        => Assert.True(ReconcileDecision.ShouldApply(
            OnboardingOutcome.Approved(BankMerchantCredential.Create("s", "u", "p"), "ref")));

    [Fact]
    public void ShouldApply_Rejected_True()
        => Assert.True(ReconcileDecision.ShouldApply(OnboardingOutcome.Rejected("x")));

    [Fact]
    public void ShouldApply_Submitted_False_ErkenTerminalYok()
        => Assert.False(ReconcileDecision.ShouldApply(OnboardingOutcome.Submitted("ref")));
}
