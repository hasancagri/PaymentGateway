using PaymentGatewayApi.Modules.BankIntegration.Banks;
using PaymentGatewayApi.Modules.BankIntegration.Banks.Enums;
using PaymentGatewayApi.Modules.BankIntegration.MerchantBanks;
using PaymentGatewayApi.Modules.BankIntegration.MerchantBanks.Enums;
using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions;
using PaymentGatewayApi.Modules.CommissionManagement.MerchantCommissions;

namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Services.BankAdapters.Abstractions;

public interface IBankSelector : IScopedDependency
{
    Task<BankRoute> SelectBestAsync(Guid merchantId, string currency, CancellationToken ct);
}

public class BankSelector : IBankSelector
{
    private readonly BankIntegrationContext _bankDb;
    private readonly CommissionManagementContext _commissionDb;

    public BankSelector(BankIntegrationContext bankDb, CommissionManagementContext commissionDb)
    {
        _bankDb = bankDb;
        _commissionDb = commissionDb;
    }

    public async Task<BankRoute> SelectBestAsync(Guid merchantId, string currency, CancellationToken ct)
    {
        var activeBanks = await _bankDb.Set<Bank>()
            .Where(b => b.Status == BankStatus.Active)
            .ToListAsync(ct);

        var eligibleBanks = activeBanks
            .Where(b => b.SupportsCurrency(currency))
            .ToList();

        if (eligibleBanks.Count == 0)
            throw new InvalidOperationException($"No active bank supports currency: {currency}");

        var eligibleBankIds = eligibleBanks.Select(b => b.Id).ToList();

        var bankCommissions = await _commissionDb.Set<BankCommission>()
            .Where(bc => eligibleBankIds.Contains(bc.BankId))
            .ToListAsync(ct);

        var merchantCommissions = await _commissionDb.Set<MerchantCommission>()
            .Where(mc => mc.MerchantId == merchantId)
            .ToListAsync(ct);

        var best = (
            from mc in merchantCommissions
            join bc in bankCommissions on mc.BankCommissionId equals bc.Id
            join bank in eligibleBanks on bc.BankId equals bank.Id
            orderby mc.Rate.Value
            select new { mc, bc, bank }
        ).FirstOrDefault();

        if (best is null)
            throw new InvalidOperationException(
                $"No commission defined for merchant {merchantId} with currency {currency}");

        var merchantBank = await _bankDb.Set<MerchantBank>()
            .FirstOrDefaultAsync(mb =>
                mb.MerchantId == merchantId &&
                mb.BankId == best.bank.Id &&
                mb.Status == MerchantBankStatus.Active, ct);

        if (merchantBank is null)
            throw new InvalidOperationException(
                $"No active credentials for merchant {merchantId} at bank {best.bank.Name.Value}");

        return new BankRoute(
            BankId: best.bank.Id,
            BankName: best.bank.Name.Value,
            BankRate: best.bc.Rate.Value,
            MerchantRate: best.mc.Rate.Value
        );
    }
}