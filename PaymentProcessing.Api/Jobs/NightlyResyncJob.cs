using Hangfire;
using PaymentGateway.SyncContracts.BankIntegration;
using PaymentGateway.SyncContracts.Commission;
using PaymentGateway.SyncContracts.Merchant;
using BankIntPageRequest = PaymentGateway.SyncContracts.BankIntegration.PageRequest;
using CommissionPageRequest = PaymentGateway.SyncContracts.Commission.PageRequest;
using MerchantPageRequest = PaymentGateway.SyncContracts.Merchant.PageRequest;

namespace PaymentProcessing.Api.Jobs;

public class NightlyResyncJob(
    SyncBankIntegrationService.SyncBankIntegrationServiceClient bankIntClient,
    SyncCommissionService.SyncCommissionServiceClient commissionClient,
    SyncMerchantService.SyncMerchantServiceClient merchantClient,
    IDocumentStore store,
    ILogger<NightlyResyncJob> logger)
{
    private const int PageSize = 100;

    [AutomaticRetry(Attempts = 3)]
    public async Task RunAsync()
    {
        logger.LogInformation("Nightly resync started");
        await ResyncMerchantBanksAsync();
        await ResyncBankCommissionsAsync();
        await ResyncMerchantCommissionsAsync();
        await ResyncMerchantsAsync();
        logger.LogInformation("Nightly resync completed");
    }

    private async Task ResyncMerchantBanksAsync()
    {
        for (var page = 0; ; page++)
        {
            var response = await bankIntClient.GetMerchantBanksAsync(
                new BankIntPageRequest { Page = page, PageSize = PageSize });

            await using var session = store.LightweightSession();
            foreach (var item in response.Items)
                session.Store(MerchantBankSummary.From(item));
            await session.SaveChangesAsync();

            if (!response.HasNextPage) break;
        }
        logger.LogInformation("MerchantBankSummary resync done");
    }

    private async Task ResyncBankCommissionsAsync()
    {
        for (var page = 0; ; page++)
        {
            var response = await commissionClient.GetBankCommissionsAsync(
                new CommissionPageRequest { Page = page, PageSize = PageSize });

            await using var session = store.LightweightSession();
            foreach (var item in response.Items)
                session.Store(BankCommissionSummary.From(item));
            await session.SaveChangesAsync();

            if (!response.HasNextPage) break;
        }
        logger.LogInformation("BankCommissionSummary resync done");
    }

    private async Task ResyncMerchantCommissionsAsync()
    {
        for (var page = 0; ; page++)
        {
            var response = await commissionClient.GetMerchantCommissionsAsync(
                new CommissionPageRequest { Page = page, PageSize = PageSize });

            await using var session = store.LightweightSession();
            foreach (var item in response.Items)
                session.Store(MerchantCommissionSummary.From(item));
            await session.SaveChangesAsync();

            if (!response.HasNextPage) break;
        }
        logger.LogInformation("MerchantCommissionSummary resync done");
    }

    private async Task ResyncMerchantsAsync()
    {
        for (var page = 0; ; page++)
        {
            var response = await merchantClient.GetMerchantsAsync(
                new MerchantPageRequest { Page = page, PageSize = PageSize });

            await using var session = store.LightweightSession();
            foreach (var item in response.Items)
                session.Store(MerchantSummary.From(item));
            await session.SaveChangesAsync();

            if (!response.HasNextPage) break;
        }
        logger.LogInformation("MerchantSummary resync done");
    }
}