namespace Payment.Api.Domains.StoreOnboarding;

/// <summary>
/// 091: StoreOnboarding altında bir (store, banka) çifti için başvuru — child entity (AggregateRoot DEĞİL,
/// base ALMAZ). Yalnız StoreOnboarding metotlarında üretilir/değişir (İlke II). Credential/sır İÇERMEZ —
/// yalnız opak CredentialRef (at-rest şifreli credential-store işareti, İlke VI). Red'ler denetim için saklanır.
/// </summary>
public class BankApplication
{
    // Marten Newtonsoft private-setter deserializasyonu için parametresiz ctor (NonPublicSetters).
    private BankApplication() { }

    public string BankCode { get; private set; } = string.Empty;
    public BankApplicationStatus Status { get; private set; }
    public string? ReasonCode { get; private set; }
    public Guid? CredentialRef { get; private set; } // onayda credential-store opak işareti; aksi null (sır değil)
    public string? BankApplicationRef { get; private set; } // banka tarafı başvuru referansı (reconcile); sır değil
    public DateTime UpdatedAt { get; private set; }

    // Yalnız StoreOnboarding.SubmitProfile/Resubmit/RecordBankOutcome çağırır (İlke II).
    internal static BankApplication From(string bankCode, OnboardingOutcome outcome, DateTime now)
    {
        var app = new BankApplication { BankCode = bankCode };
        app.Apply(outcome, null, now);
        return app;
    }

    // Outcome'u başvuru durumuna düzleştirir. credentialRef yalnız Approved yolunda (handler credential-store'a
    // yazıp Guid verir) dolu olur.
    internal void Apply(OnboardingOutcome outcome, Guid? credentialRef, DateTime now)
    {
        Status = outcome.Kind switch
        {
            OnboardingOutcomeKind.Approved => BankApplicationStatus.Approved,
            OnboardingOutcomeKind.Rejected => BankApplicationStatus.Rejected,
            _ => BankApplicationStatus.Referred
        };
        ReasonCode = outcome.ReasonCode;
        if (outcome.BankApplicationRef is not null) BankApplicationRef = outcome.BankApplicationRef;
        if (credentialRef is not null) CredentialRef = credentialRef;
        UpdatedAt = now;
    }
}
