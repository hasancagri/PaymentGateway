namespace CommissionManagement.Api.Domains.BankCommissions.Features.Commands;

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
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var rateResult = CommissionRate.Create(cmd.Rate);
            if (!rateResult.IsSuccess)
                return FeatureObjectResultModel<DefineBankCommissionResponse>.Error(rateResult.Messages!);

            var criteria   = new CommissionCriteria(cmd.CardBrand, cmd.CardType, cmd.TransactionRegion);
            var commission = BankCommission.Define(cmd.BankId, criteria, rateResult.Data!);
            session.Store(commission);

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