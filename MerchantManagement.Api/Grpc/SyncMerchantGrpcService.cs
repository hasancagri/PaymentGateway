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
}