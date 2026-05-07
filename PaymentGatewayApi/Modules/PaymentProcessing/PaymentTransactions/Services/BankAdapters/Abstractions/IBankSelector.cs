using PaymentGatewayApi.Modules.BankIntegration.Banks;
using PaymentGatewayApi.Modules.BankIntegration.Banks.Enums;
using PaymentGatewayApi.Modules.BankIntegration.MerchantBanks;
using PaymentGatewayApi.Modules.BankIntegration.MerchantBanks.Enums;
using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions;
using PaymentGatewayApi.Modules.CommissionManagement.MerchantCommissions;
using PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.ValueObjects;

namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Services.BankAdapters.Abstractions;

public interface IBankSelector : IScopedDependency
{
    Task<BankRoute> SelectBestAsync(Guid merchantId, string currency, CardProfile cardProfile, CancellationToken ct);
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

    public async Task<BankRoute> SelectBestAsync(
        Guid merchantId, string currency, CardProfile cardProfile, CancellationToken ct)
    {
        var merchantBanks = await _bankDb.Set<MerchantBank>()
            .Where(mb => mb.MerchantId == merchantId && mb.Status == MerchantBankStatus.Active)
            .ToListAsync(ct);

        if (merchantBanks.Count == 0)
            throw new InvalidOperationException($"No active merchant banks for merchant {merchantId}");

        var merchantBankIds = merchantBanks.Select(mb => mb.BankId).ToList();

        var eligibleBanks = (await _bankDb.Set<Bank>()
            .Where(b => b.Status == BankStatus.Active && merchantBankIds.Contains(b.Id))
            .ToListAsync(ct))
            .Where(b => b.SupportsCurrency(currency))
            .ToList();

        if (eligibleBanks.Count == 0)
            throw new InvalidOperationException($"No active bank supports currency: {currency}");

        var eligibleBankIds = eligibleBanks.Select(b => b.Id).ToList();

        var bankCommissions = (await _commissionDb.Set<BankCommission>()
            .Where(bc => eligibleBankIds.Contains(bc.BankId))
            .ToListAsync(ct))
            .Where(bc =>
                bc.Criteria.CardBrand == cardProfile.CardBrand &&
                bc.Criteria.CardType == cardProfile.CardType &&
                bc.Criteria.TransactionRegion == cardProfile.TransactionRegion)
            .ToList();

        if (bankCommissions.Count == 0)
            throw new InvalidOperationException(
                $"No commission found for criteria: {cardProfile.CardBrand}/{cardProfile.CardType}/{cardProfile.TransactionRegion}");

        var bankCommissionIds = bankCommissions.Select(bc => bc.Id).ToList();

        var merchantCommissions = await _commissionDb.Set<MerchantCommission>()
            .Where(mc => mc.MerchantId == merchantId && bankCommissionIds.Contains(mc.BankCommissionId))
            .ToListAsync(ct);

        if (merchantCommissions.Count == 0)
            throw new InvalidOperationException(
                $"No merchant commission defined for merchant {merchantId} matching card criteria");

        var candidates = (
            from mc in merchantCommissions
            join bc in bankCommissions on mc.BankCommissionId equals bc.Id
            join bank in eligibleBanks on bc.BankId equals bank.Id
            select new { mc, bc, bank }
        ).ToList();

        var selected = candidates.FirstOrDefault(c =>
                !string.IsNullOrEmpty(cardProfile.IssuingMemberId) &&
                c.bank.IcaMemberId == cardProfile.IssuingMemberId)
            ?? candidates.OrderBy(c => c.mc.Rate.Value).First();

        return new BankRoute(
            BankId: selected.bank.Id,
            BankName: selected.bank.Name.Value,
            BankRate: selected.bc.Rate.Value,
            MerchantRate: selected.mc.Rate.Value
        );
    }
}