using Grpc.Core;
using PaymentGateway.SyncContracts.BankIntegration;
using PaymentGatewayApi.Modules.BankIntegration.MerchantBanks.Enums;

namespace BankIntegration.Api.Grpc;

public class SyncBankIntegrationGrpcService(IQuerySession session)
    : SyncBankIntegrationService.SyncBankIntegrationServiceBase
{
    private const int DefaultPageSize = 100;

    public override async Task<MerchantBanksPage> GetMerchantBanks(
        PageRequest request, ServerCallContext context)
    {
        var pageSize = request.PageSize > 0 ? request.PageSize : DefaultPageSize;
        var skip = request.Page * pageSize;

        var merchantBanks = await session.Query<MerchantBank>()
            .Skip(skip)
            .Take(pageSize + 1)
            .ToListAsync(context.CancellationToken);

        var hasNextPage = merchantBanks.Count > pageSize;
        var pageBanks = merchantBanks.Take(pageSize).ToList();

        var bankIds = pageBanks.Select(mb => mb.BankId).Distinct().ToList();
        var banks = await session.Query<Bank>()
            .Where(b => bankIds.Contains(b.Id))
            .ToListAsync(context.CancellationToken);
        var bankDict = banks.ToDictionary(b => b.Id);

        var response = new MerchantBanksPage { HasNextPage = hasNextPage };
        foreach (var mb in pageBanks)
        {
            bankDict.TryGetValue(mb.BankId, out var bank);
            var item = new MerchantBankItem
            {
                MerchantBankId = mb.Id.ToString(),
                MerchantId = mb.MerchantId.ToString(),
                BankId = mb.BankId.ToString(),
                BankName = bank?.Name.Value ?? string.Empty,
                IcaMemberId = bank?.IcaMemberId ?? string.Empty,
                IsActive = mb.Status == MerchantBankStatus.Active && bank?.Status == BankStatus.Active
            };
            item.SupportedCurrencies.AddRange(bank?.SupportedCurrencies ?? []);
            response.Items.Add(item);
        }

        return response;
    }
}