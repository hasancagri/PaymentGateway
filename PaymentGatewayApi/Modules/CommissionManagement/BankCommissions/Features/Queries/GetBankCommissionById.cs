using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.Enums;

namespace PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.Features.Queries;

public static class GetBankCommissionById
{
    public class GetBankCommissionByIdQuery
    {
        public required Guid CommissionId { get; set; }
    }

    public class GetBankCommissionByIdResponse
    {
        public Guid Id { get; set; }
        public Guid BankId { get; set; }
        public decimal Rate { get; set; }
        public CardBrand CardBrand { get; set; }
        public CardType CardType { get; set; }
        public TransactionRegion TransactionRegion { get; set; }
    }

    public class GetBankCommissionByIdHandler
    {
        public async Task<FeatureObjectResultModel<GetBankCommissionByIdResponse>> Handle(
            GetBankCommissionByIdQuery query,
            CommissionManagementContext db,
            CancellationToken ct)
        {
            var commission = await db.Set<BankCommission>().FirstOrDefaultAsync(x => x.Id == query.CommissionId, ct);
            if (commission is null)
                return FeatureObjectResultModel<GetBankCommissionByIdResponse>.Error(new MessageItem
                {
                    Table = nameof(BankCommission),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            return FeatureObjectResultModel<GetBankCommissionByIdResponse>.Ok(new GetBankCommissionByIdResponse
            {
                Id = commission.Id,
                BankId = commission.BankId,
                Rate = commission.Rate.Value,
                CardBrand = commission.Criteria.CardBrand,
                CardType = commission.Criteria.CardType,
                TransactionRegion = commission.Criteria.TransactionRegion
            });
        }
    }
}