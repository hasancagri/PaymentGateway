namespace PaymentProcessing.Api.Domains.BinRecords.Features.Commands;

public static class CreateBinRecord
{
    public class CreateBinRecordCommand
    {
        public required string BinStart { get; set; }
        public required string BinEnd { get; set; }
        public required string CardBrand { get; set; }
        public required string CardProductType { get; set; }
        public required string BinCountry { get; set; }
        public required string BinRegion { get; set; }
    }

    public class CreateBinRecordResponse
    {
        public Guid Id { get; set; }
    }

    public class CreateBinRecordHandler
    {
        public async Task<FeatureObjectResultModel<CreateBinRecordResponse>> Handle(
            CreateBinRecordCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var recordResult = BinRecord.Create(
                cmd.BinStart, cmd.BinEnd,
                cmd.CardBrand, cmd.CardProductType,
                cmd.BinCountry, cmd.BinRegion);

            if (!recordResult.IsSuccess)
                return FeatureObjectResultModel<CreateBinRecordResponse>.Error(recordResult.Messages!);

            session.Store(recordResult.Data!);
            return FeatureObjectResultModel<CreateBinRecordResponse>.Ok(
                new CreateBinRecordResponse { Id = recordResult.Data!.Id });
        }
    }
}