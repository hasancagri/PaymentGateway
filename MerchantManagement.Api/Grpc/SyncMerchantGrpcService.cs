using Grpc.Core;
using MerchantManagement.Api.Domains.Merchants;
using PaymentGateway.SyncContracts.Merchant;

namespace MerchantManagement.Api.Grpc;

public class SyncMerchantGrpcService(IQuerySession session)
    : SyncMerchantService.SyncMerchantServiceBase
{
    private const int DefaultPageSize = 100;

    public override async Task<MerchantsPage> GetMerchants(
        PageRequest request, ServerCallContext context)
    {
        var pageSize = request.PageSize > 0 ? request.PageSize : DefaultPageSize;
        var skip = request.Page * pageSize;

        var merchants = await session.Query<Merchant>()
            .Skip(skip)
            .Take(pageSize + 1)
            .ToListAsync(context.CancellationToken);

        var hasNextPage = merchants.Count > pageSize;
        var page = merchants.Take(pageSize).ToList();

        var response = new MerchantsPage { HasNextPage = hasNextPage };
        foreach (var m in page)
        {
            response.Items.Add(new MerchantItem
            {
                MerchantId = m.Id.ToString(),
                WebhookUrl = m.WebhookUrl.Value,
                IsActive = m.Status == MerchantStatus.Active
            });
        }

        return response;
    }

    public override async Task<MerchantApiKeyResponse> GetMerchantByApiKey(
        ApiKeyRequest request, ServerCallContext context)
    {
        var merchant = await session.Query<Merchant>()
            .FirstOrDefaultAsync(
                m => m.ApiKeys.Any(k => k.KeyValue.Hash == request.KeyHash),
                context.CancellationToken);

        if (merchant is null)
            return new MerchantApiKeyResponse { Found = false };

        var key = merchant.ApiKeys.FirstOrDefault(k => k.KeyValue.Hash == request.KeyHash);
        if (key is null)
            return new MerchantApiKeyResponse { Found = false };

        return new MerchantApiKeyResponse
        {
            Found = true,
            MerchantId = merchant.Id.ToString(),
            MerchantName = merchant.Name.Value,
            MerchantStatus = (int)merchant.Status,
            KeyStatus = (int)key.Status
        };
    }
}