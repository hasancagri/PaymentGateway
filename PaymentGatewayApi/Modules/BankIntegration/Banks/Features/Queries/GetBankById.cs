namespace PaymentGatewayApi.Modules.BankIntegration.Banks.Features.Queries;

public static class GetBankById
{
    public class GetBankByIdQuery
    {
        public required Guid BankId { get; set; }
    }

    public class GetBankByIdResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Priority { get; set; }
        public string Status { get; set; }
        public IReadOnlyCollection<string> SupportedCurrencies { get; set; }
    }

    public class GetBankByIdHandler
    {
        public async Task<FeatureObjectResultModel<GetBankByIdResponse>> Handle(
            GetBankByIdQuery query,
            BankIntegrationContext db,
            CancellationToken ct)
        {
            var bank = await db.Set<Bank>().FirstOrDefaultAsync(x => x.Id == query.BankId, ct);
            if (bank is null)
                return FeatureObjectResultModel<GetBankByIdResponse>.Error(new MessageItem
                {
                    Table = nameof(Bank),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            return FeatureObjectResultModel<GetBankByIdResponse>.Ok(new GetBankByIdResponse
            {
                Id = bank.Id,
                Name = bank.Name.Value,
                Priority = bank.Priority.Value,
                Status = bank.Status.ToString(),
                SupportedCurrencies = bank.SupportedCurrencies
            });
        }
    }
}