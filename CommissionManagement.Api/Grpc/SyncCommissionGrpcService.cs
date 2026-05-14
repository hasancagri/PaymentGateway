using Grpc.Core;
using PaymentGateway.SyncContracts.Commission;

namespace CommissionManagement.Api.Grpc;

public class SyncCommissionGrpcService(IQuerySession session)
    : SyncCommissionService.SyncCommissionServiceBase
{
    private const int DefaultPageSize = 100;

    public override async Task<BankCommissionsPage> GetBankCommissions(
        PageRequest request, ServerCallContext context)
    {
        var pageSize = request.PageSize > 0 ? request.PageSize : DefaultPageSize;
        var skip = request.Page * pageSize;

        var commissions = await session.Query<BankCommission>()
            .Skip(skip)
            .Take(pageSize + 1)
            .ToListAsync(context.CancellationToken);

        var hasNextPage = commissions.Count > pageSize;
        var page = commissions.Take(pageSize).ToList();

        var response = new BankCommissionsPage { HasNextPage = hasNextPage };
        foreach (var c in page)
        {
            response.Items.Add(new BankCommissionItem
            {
                BankCommissionId = c.Id.ToString(),
                BankId = c.BankId.ToString(),
                CardBrand = (int)c.Criteria.CardBrand,
                CardType = (int)c.Criteria.CardType,
                TransactionRegion = (int)c.Criteria.TransactionRegion,
                Rate = c.Rate.Value.ToString()
            });
        }

        return response;
    }

    public override async Task<MerchantCommissionsPage> GetMerchantCommissions(
        PageRequest request, ServerCallContext context)
    {
        var pageSize = request.PageSize > 0 ? request.PageSize : DefaultPageSize;
        var skip = request.Page * pageSize;

        var commissions = await session.Query<MerchantCommission>()
            .Skip(skip)
            .Take(pageSize + 1)
            .ToListAsync(context.CancellationToken);

        var hasNextPage = commissions.Count > pageSize;
        var page = commissions.Take(pageSize).ToList();

        var response = new MerchantCommissionsPage { HasNextPage = hasNextPage };
        foreach (var c in page)
        {
            response.Items.Add(new MerchantCommissionItem
            {
                MerchantCommissionId = c.Id.ToString(),
                MerchantId = c.MerchantId.ToString(),
                BankCommissionId = c.BankCommissionId.ToString(),
                CardBrand = (int)c.Criteria.CardBrand,
                CardType = (int)c.Criteria.CardType,
                TransactionRegion = (int)c.Criteria.TransactionRegion,
                Rate = c.Rate.Value.ToString()
            });
        }

        return response;
    }
}