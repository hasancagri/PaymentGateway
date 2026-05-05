using PaymentGatewayApi.Modules.BankIntegration.Banks.ValueObjects;
using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.BankIntegration.Banks.Features.Commands;

public static class ConfigureBank
{
    public class ConfigureBankCommand
    {
        public required string Name { get; set; }
        public required int Priority { get; set; }
        public required string ApiUrl { get; set; }
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
            BankIntegrationContext db,
            CancellationToken ct)
        {
            var bankResult = Bank.Configure(cmd.Name, cmd.Priority, cmd.ApiUrl);
            if (!bankResult.IsSuccess)
                return FeatureObjectResultModel<ConfigureBankResponse>.Error(bankResult.Messages!);

            await db.Set<Bank>().AddAsync(bankResult.Data!, ct);
            return FeatureObjectResultModel<ConfigureBankResponse>.Ok(new ConfigureBankResponse { Id = bankResult.Data!.Id });
        }
    }
}