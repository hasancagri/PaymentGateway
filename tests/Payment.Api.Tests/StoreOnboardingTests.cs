using Payment.Api.Constants;
using Payment.Api.Domains.StoreOnboarding;
using Payment.Api.Domains.StoreOnboarding.BankOnboarding;
using Payment.Api.Domains.StoreOnboarding.ValueObjects;

namespace Payment.Api.Tests;

// 091 (İlke VIII Domain-TDD): StoreOnboarding aggregate davranışı — fan-out, ilk-onay→Live, çift-onay no-op,
// red izolasyonu, resubmit.
public class StoreOnboardingTests
{
    private const string ValidIban = "TR330006100519786457841326";

    private static StoreOnboarding NewOnboarding()
        => StoreOnboarding.Create(Guid.NewGuid(), "corr-1", "Acme Store", "open-tok", "cb-tok",
            DateTime.UtcNow.AddHours(1)).Data!;

    private static StoreProfile Profile()
        => StoreProfile.Create("Acme A.Ş.", "1234567890", ValidIban, "5411", "https://acme.example", "Ada", "ada@acme.example").Data!;

    private static List<(string, OnboardingOutcome)> Fanout(params string[] banks)
        => banks.Select(b => (b, OnboardingOutcome.Submitted($"{b}-ref"))).ToList();

    [Fact]
    public void SubmitProfile_FanOut_HerBankaReferred_StatusSubmitted()
    {
        var o = NewOnboarding();
        var r = o.SubmitProfile(Profile(), Fanout("MAVIBANK", "SARIBANK"), DateTime.UtcNow);

        Assert.True(r.IsSuccess);
        Assert.Equal(OnboardingStatus.Submitted, o.Status);
        Assert.Equal(2, o.Applications.Count);
        Assert.All(o.Applications, a => Assert.Equal(BankApplicationStatus.Referred, a.Status));
    }

    [Fact]
    public void SubmitProfile_BankaOzelEksik_OBankaRejected_DigerleriReferred()
    {
        var o = NewOnboarding();
        var fanout = new List<(string, OnboardingOutcome)>
        {
            ("MAVIBANK", OnboardingOutcome.Submitted("MAVIBANK-ref")),
            ("SARIBANK", OnboardingOutcome.Rejected(StoreOnboardingResourceConstants.BANK_FIELD_MISSING))
        };
        o.SubmitProfile(Profile(), fanout, DateTime.UtcNow);

        Assert.Equal(BankApplicationStatus.Referred, o.Applications.First(a => a.BankCode == "MAVIBANK").Status);
        Assert.Equal(BankApplicationStatus.Rejected, o.Applications.First(a => a.BankCode == "SARIBANK").Status);
    }

    [Fact]
    public void SubmitProfile_DraftDegil_AlreadySubmitted()
    {
        var o = NewOnboarding();
        o.SubmitProfile(Profile(), Fanout("MAVIBANK"), DateTime.UtcNow);
        var r = o.SubmitProfile(Profile(), Fanout("MAVIBANK"), DateTime.UtcNow);

        Assert.False(r.IsSuccess);
        Assert.Equal(StoreOnboardingResourceConstants.ALREADY_SUBMITTED, r.Messages!.First().Code);
    }

    [Fact]
    public void SubmitProfile_SureDolmus_LinkExpired()
    {
        var o = StoreOnboarding.Create(Guid.NewGuid(), "corr", "Store", "ot", "cb", DateTime.UtcNow.AddHours(1)).Data!;
        var r = o.SubmitProfile(Profile(), Fanout("MAVIBANK"), DateTime.UtcNow.AddHours(2)); // now > ExpiresAt

        Assert.False(r.IsSuccess);
        Assert.Equal(StoreOnboardingResourceConstants.LINK_EXPIRED, r.Messages!.First().Code);
    }

    [Fact]
    public void RecordBankOutcome_IlkOnay_StoreLive_CredentialRefSet()
    {
        var o = NewOnboarding();
        o.SubmitProfile(Profile(), Fanout("MAVIBANK", "SARIBANK"), DateTime.UtcNow);

        var credRef = Guid.NewGuid();
        var r = o.RecordBankOutcome("MAVIBANK", Approved(), credRef, DateTime.UtcNow);

        Assert.True(r.IsSuccess);
        Assert.Equal(OnboardingStatus.Live, o.Status);
        var app = o.Applications.First(a => a.BankCode == "MAVIBANK");
        Assert.Equal(BankApplicationStatus.Approved, app.Status);
        Assert.Equal(credRef, app.CredentialRef);
    }

    [Fact]
    public void RecordBankOutcome_IkinciBankaOnay_LiveKalir()
    {
        var o = NewOnboarding();
        o.SubmitProfile(Profile(), Fanout("MAVIBANK", "SARIBANK"), DateTime.UtcNow);
        o.RecordBankOutcome("MAVIBANK", Approved(), Guid.NewGuid(), DateTime.UtcNow);
        o.RecordBankOutcome("SARIBANK", Approved(), Guid.NewGuid(), DateTime.UtcNow);

        Assert.Equal(OnboardingStatus.Live, o.Status);
        Assert.Equal(2, o.Applications.Count(a => a.Status == BankApplicationStatus.Approved));
    }

    [Fact]
    public void RecordBankOutcome_AyniBankaTekrarOnay_NoOp()
    {
        var o = NewOnboarding();
        o.SubmitProfile(Profile(), Fanout("MAVIBANK"), DateTime.UtcNow);
        var ref1 = Guid.NewGuid();
        o.RecordBankOutcome("MAVIBANK", Approved(), ref1, DateTime.UtcNow);
        var r = o.RecordBankOutcome("MAVIBANK", Approved(), Guid.NewGuid(), DateTime.UtcNow);

        Assert.True(r.IsSuccess);
        Assert.Equal(OnboardingStatus.Live, o.Status);
        // İlk credentialRef korunur (ikinci onay no-op — çifte webhook/reconcile).
        Assert.Equal(ref1, o.Applications.First(a => a.BankCode == "MAVIBANK").CredentialRef);
    }

    [Fact]
    public void RecordBankOutcome_RedIzolasyonu_LiveEtkilenmez()
    {
        var o = NewOnboarding();
        o.SubmitProfile(Profile(), Fanout("MAVIBANK", "SARIBANK"), DateTime.UtcNow);
        o.RecordBankOutcome("MAVIBANK", Approved(), Guid.NewGuid(), DateTime.UtcNow); // Live
        o.RecordBankOutcome("SARIBANK", OnboardingOutcome.Rejected("x"), null, DateTime.UtcNow);

        Assert.Equal(OnboardingStatus.Live, o.Status); // red store Live'ını düşürmez
        Assert.Equal(BankApplicationStatus.Approved, o.Applications.First(a => a.BankCode == "MAVIBANK").Status);
        Assert.Equal(BankApplicationStatus.Rejected, o.Applications.First(a => a.BankCode == "SARIBANK").Status);
    }

    [Fact]
    public void Resubmit_ReddedilenBanka_Referred()
    {
        var o = NewOnboarding();
        o.SubmitProfile(Profile(), new List<(string, OnboardingOutcome)>
        {
            ("MAVIBANK", OnboardingOutcome.Rejected("x"))
        }, DateTime.UtcNow);

        var r = o.Resubmit("MAVIBANK", OnboardingOutcome.Submitted("MAVIBANK-ref2"), DateTime.UtcNow);

        Assert.True(r.IsSuccess);
        Assert.Equal(BankApplicationStatus.Referred, o.Applications.First(a => a.BankCode == "MAVIBANK").Status);
    }

    private static OnboardingOutcome Approved()
        => OnboardingOutcome.Approved(BankMerchantCredential.Create("SHOP", "USER", "PASS"), "ref");
}
