using PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.ValueObjects;

namespace PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Services.BankAdapters.Abstractions;

public interface IBankSelector : IScopedDependency
{
    Task<BankRoute> SelectBestAsync(Guid merchantId, string currency, CardProfile cardProfile, CancellationToken ct);
}

// TODO: Task 9 will refactor BankSelector to use local BankRouteSummary Marten document
public class BankSelector : IBankSelector
{
    public Task<BankRoute> SelectBestAsync(
        Guid merchantId, string currency, CardProfile cardProfile, CancellationToken ct)
    {
        throw new NotImplementedException("BankSelector will be implemented in Task 9 using local BankRouteSummary read model.");
    }
}