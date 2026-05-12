namespace PaymentProcessing.Api.Domains.PaymentTransactions.Services.BankAdapters.Abstractions;

public interface IBankSelector : IScopedDependency
{
    // Task<BankRoute> SelectBestAsync(Guid merchantId, string currency, CardProfile cardProfile, CancellationToken ct);
}

public class BankSelector(IQuerySession session) : IBankSelector
{
    // public async Task<BankRoute> SelectBestAsync(
    //     Guid merchantId, string currency, CardProfile cardProfile, CancellationToken ct)
    // {
    //     var merchantBanks = await session.Query<MerchantBankSummary>()
    //         .Where(mb => mb.MerchantId == merchantId
    //                   && mb.IsActive
    //                   && mb.SupportedCurrencies.Contains(currency))
    //         .ToListAsync(token: ct);
    //
    //     if (merchantBanks.Count == 0)
    //         throw new InvalidOperationException($"No active merchant banks for merchant {merchantId}");
    //
    //     var eligibleBankIds = merchantBanks.Select(mb => mb.BankId).ToList();
    //
    //     var cardBrand = cardProfile.CardBrand.ToString();
    //     var cardType = cardProfile.CardType.ToString();
    //     var transactionRegion = cardProfile.TransactionRegion.ToString();
    //
    //     var bankCommissions = await session.Query<BankCommissionSummary>()
    //         .Where(bc => eligibleBankIds.Contains(bc.BankId)
    //                   && bc.CardBrand == cardBrand
    //                   && bc.CardType == cardType
    //                   && bc.TransactionRegion == transactionRegion)
    //         .ToListAsync(token: ct);
    //
    //     if (bankCommissions.Count == 0)
    //         throw new InvalidOperationException(
    //             $"No commission found for criteria: {cardProfile.CardBrand}/{cardProfile.CardType}/{cardProfile.TransactionRegion}");
    //
    //     var bankCommissionIds = bankCommissions.Select(bc => bc.Id).ToList();
    //
    //     var merchantCommissions = await session.Query<MerchantCommissionSummary>()
    //         .Where(mc => mc.MerchantId == merchantId && bankCommissionIds.Contains(mc.BankCommissionId))
    //         .ToListAsync(token: ct);
    //
    //     if (merchantCommissions.Count == 0)
    //         throw new InvalidOperationException(
    //             $"No merchant commission defined for merchant {merchantId} matching card criteria");
    //
    //     var candidates = (
    //         from mc in merchantCommissions
    //         join bc in bankCommissions on mc.BankCommissionId equals bc.Id
    //         join mb in merchantBanks on bc.BankId equals mb.BankId
    //         select new { mc, bc, mb }
    //     ).ToList();
    //
    //     var selected = candidates.FirstOrDefault(c =>
    //             !string.IsNullOrEmpty(cardProfile.IssuingMemberId) &&
    //             c.mb.IcaMemberId == cardProfile.IssuingMemberId)
    //         ?? candidates.OrderBy(c => c.mc.Rate).First();
    //
    //     return new BankRoute(
    //         BankId: selected.mb.BankId,
    //         BankName: selected.mb.BankName,
    //         BankRate: selected.bc.Rate,
    //         MerchantRate: selected.mc.Rate);
    // }
}