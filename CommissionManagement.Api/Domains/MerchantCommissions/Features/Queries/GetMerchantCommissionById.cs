namespace CommissionManagement.Api.Domains.MerchantCommissions.Features.Queries;

public static class GetMerchantCommissionById
{
    public class GetMerchantCommissionByIdQuery
    {
        public required Guid CommissionId { get; set; }
    }

    public class GetMerchantCommissionByIdResponse
    {
        public Guid Id { get; set; }
        public Guid MerchantId { get; set; }
        public Guid BankCommissionId { get; set; }
        public decimal Rate { get; set; }
        public CardBrand CardBrand { get; set; }
        public CardType CardType { get; set; }
        public TransactionRegion TransactionRegion { get; set; }
    }

    public class GetMerchantCommissionByIdHandler
    {
        public async Task<FeatureObjectResultModel<GetMerchantCommissionByIdResponse>> Handle(
            GetMerchantCommissionByIdQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var commission = await session.LoadAsync<MerchantCommission>(query.CommissionId, ct);
            if (commission is null)
                return FeatureObjectResultModel<GetMerchantCommissionByIdResponse>.Error(new MessageItem
                {
                    Table = nameof(MerchantCommission),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            return FeatureObjectResultModel<GetMerchantCommissionByIdResponse>.Ok(new GetMerchantCommissionByIdResponse
            {
                Id = commission.Id,
                MerchantId = commission.MerchantId,
                BankCommissionId = commission.BankCommissionId,
                Rate = commission.Rate.Value,
                CardBrand = commission.Criteria.CardBrand,
                CardType = commission.Criteria.CardType,
                TransactionRegion = commission.Criteria.TransactionRegion
            });
        }
    }
}