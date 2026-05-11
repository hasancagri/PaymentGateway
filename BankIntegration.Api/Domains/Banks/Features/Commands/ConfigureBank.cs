namespace BankIntegration.Api.Domains.Banks.Features.Commands;

public static class ConfigureBank
{
    public class ConfigureBankCommand
    {
        public required string Name { get; set; }
        public required int Priority { get; set; }
        public required string ApiUrl { get; set; }
        public string? IcaMemberId { get; set; }
    }

    public class ConfigureBankResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class ConfigureBankHandler
    {
        public async Task<FeatureObjectResultModel<ConfigureBankResponse>> Handle(
            ConfigureBankCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var bankResult = Bank.Configure(cmd.Name, cmd.Priority, cmd.ApiUrl, cmd.IcaMemberId);
            if (!bankResult.IsSuccess)
                return FeatureObjectResultModel<ConfigureBankResponse>.Error(bankResult.Messages!);

            session.Store(bankResult.Data!);
            return FeatureObjectResultModel<ConfigureBankResponse>.Ok(new ConfigureBankResponse { Id = bankResult.Data!.Id });
        }
    }
}