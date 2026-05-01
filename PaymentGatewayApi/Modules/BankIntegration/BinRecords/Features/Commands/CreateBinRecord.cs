using PaymentGatewayApi.Modules.BankIntegration.BinRecords.ValueObjects;
using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.BankIntegration.BinRecords.Features.Commands;

public static class CreateBinRecord
{
    public class CreateBinRecordCommand
    {
        public required string BinStart { get; set; }
        public required string BinEnd { get; set; }
        public required string CardBrand { get; set; }
        public required string CardType { get; set; }
        public required string IssuingCountry { get; set; }
        public required string Region { get; set; }
    }

    public class CreateBinRecordResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateBinRecordHandler
    {
        public async Task<FeatureObjectResultModel<CreateBinRecordResponse>> Handle(
            CreateBinRecordCommand cmd,
            BankIntegrationContext db,
            CancellationToken ct)
        {
            var binRangeResult = BinRange.Create(cmd.BinStart, cmd.BinEnd);
            var cardInfoResult = BinCardInfo.Create(cmd.CardBrand, cmd.CardType, cmd.IssuingCountry, cmd.Region);

            var errors = new List<MessageItem>();
            if (!binRangeResult.IsSuccess) errors.AddRange(binRangeResult.Messages!);
            if (!cardInfoResult.IsSuccess) errors.AddRange(cardInfoResult.Messages!);
            if (errors.Count > 0)
                return FeatureObjectResultModel<CreateBinRecordResponse>.Error(errors);

            var record = BinRecord.Create(binRangeResult.Data!, cardInfoResult.Data!);
            await db.Set<BinRecord>().AddAsync(record, ct);
            return FeatureObjectResultModel<CreateBinRecordResponse>.Ok(new CreateBinRecordResponse { Id = record.Id });
        }
    }
}