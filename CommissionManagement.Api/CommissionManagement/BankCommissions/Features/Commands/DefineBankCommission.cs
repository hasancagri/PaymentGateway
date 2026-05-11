using PaymentGateway.SharedContracts.CommissionEvents;
using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.Enums;
using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.ValueObjects;

namespace PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.Features.Commands;

public static class DefineBankCommission
{
    public class DefineBankCommissionCommand
    {
        public required Guid BankId { get; set; }
        public required CardBrand CardBrand { get; set; }
        public required CardType CardType { get; set; }
        public required TransactionRegion TransactionRegion { get; set; }
        public required decimal Rate { get; set; }
    }

    public class DefineBankCommissionResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class DefineBankCommissionHandler
    {
        public async Task<FeatureObjectResultModel<DefineBankCommissionResponse>> Handle(
            DefineBankCommissionCommand cmd,
            CommissionManagementContext db,
            IMessageBus bus,
            CancellationToken ct)
        {
            var rateResult = CommissionRate.Create(cmd.Rate);
            if (!rateResult.IsSuccess)
                return FeatureObjectResultModel<DefineBankCommissionResponse>.Error(rateResult.Messages!);

            var criteria   = new CommissionCriteria(cmd.CardBrand, cmd.CardType, cmd.TransactionRegion);
            var commission = BankCommission.Define(cmd.BankId, criteria, rateResult.Data!);
            await db.Set<BankCommission>().AddAsync(commission, ct);

            await bus.PublishAsync(new BankCommissionSynced(
                commission.Id,
                commission.BankId,
                commission.Criteria.CardBrand.ToString(),
                commission.Criteria.CardType.ToString(),
                commission.Criteria.TransactionRegion.ToString(),
                commission.Rate.Value,
                DateTime.UtcNow));

            return FeatureObjectResultModel<DefineBankCommissionResponse>.Ok(new DefineBankCommissionResponse { Id = commission.Id });
        }
    }
}