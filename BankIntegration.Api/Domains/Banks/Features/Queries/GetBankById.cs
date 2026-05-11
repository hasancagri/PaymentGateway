namespace BankIntegration.Api.Domains.Banks.Features.Queries;

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
            IQuerySession session,
            CancellationToken ct)
        {
            var bank = await session.LoadAsync<Bank>(query.BankId, ct);
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