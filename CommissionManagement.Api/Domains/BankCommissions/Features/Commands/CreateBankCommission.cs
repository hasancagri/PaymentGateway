namespace CommissionManagement.Api.Domains.BankCommissions.Features.Commands;

public static class CreateBankCommission
{
    public class CreateBankCommissionCommand
    {
        public required Guid BankId { get; set; }
        public required CardBrand CardBrand { get; set; }
        public required CardType CardType { get; set; }
        public required TransactionRegion TransactionRegion { get; set; }
        public required decimal Rate { get; set; }
    }

    public class CreateBankCommissionResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateBankCommissionHandler
    {
        public async Task<FeatureObjectResultModel<CreateBankCommissionResponse>> Handle(
            CreateBankCommissionCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var rateResult = CommissionRate.Create(cmd.Rate);
            if (!rateResult.IsSuccess)
                return FeatureObjectResultModel<CreateBankCommissionResponse>.Error(rateResult.Messages!);

            var criteria   = new CommissionCriteria(cmd.CardBrand, cmd.CardType, cmd.TransactionRegion);
            var commission = BankCommission.Create(cmd.BankId, criteria, rateResult.Data!);
            session.Store(commission);

            await bus.PublishAsync(new BankCommissionSynced(
                commission.Id,
                commission.BankId,
                commission.Criteria.CardBrand,
                commission.Criteria.CardType,
                commission.Criteria.TransactionRegion,
                commission.Rate.Value,
                DateTime.UtcNow));

            return FeatureObjectResultModel<CreateBankCommissionResponse>.Ok(new CreateBankCommissionResponse { Id = commission.Id });
        }
    }
}