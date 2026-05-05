using PaymentGatewayApi.Modules.BankIntegration.MerchantBanks;
using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.BankIntegration.MerchantBanks.Features.Commands;

public static class AssignMerchantBank
{
    public class AssignMerchantBankCommand
    {
        public required Guid BankId { get; set; }
        public required Guid MerchantId { get; set; }
        public required string MerchantCode { get; set; }
        public required string TerminalCode { get; set; }
    }

    public class AssignMerchantBankResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class AssignMerchantBankHandler
    {
        public async Task<FeatureObjectResultModel<AssignMerchantBankResponse>> Handle(
            AssignMerchantBankCommand cmd,
            BankIntegrationContext db,
            CancellationToken ct)
        {
            var result = MerchantBank.Assign(cmd.MerchantId, cmd.BankId, cmd.MerchantCode, cmd.TerminalCode);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<AssignMerchantBankResponse>.Error(result.Messages!);

            await db.Set<MerchantBank>().AddAsync(result.Data!, ct);

            return FeatureObjectResultModel<AssignMerchantBankResponse>.Ok(
                new AssignMerchantBankResponse { Id = result.Data!.Id });
        }
    }
}