namespace PaymentGatewayApi.Modules.PaymentProcessing.BinRecords.Features.Queries;

public static class GetBinRecordById
{
    public class GetBinRecordByIdQuery
    {
        public required Guid BinRecordId { get; set; }
    }

    public class GetBinRecordByIdResponse
    {
        public Guid Id { get; set; }
        public string BinStart { get; set; }
        public string BinEnd { get; set; }
        public string CardBrand { get; set; }
        public string CardType { get; set; }
        public string IssuingCountry { get; set; }
        public string Region { get; set; }
    }

    public class GetBinRecordByIdHandler
    {
        public async Task<FeatureObjectResultModel<GetBinRecordByIdResponse>> Handle(
            GetBinRecordByIdQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var record = await session.LoadAsync<BinRecord>(query.BinRecordId, ct);
            if (record is null)
                return FeatureObjectResultModel<GetBinRecordByIdResponse>.Error(new MessageItem
                {
                    Table = nameof(BinRecord),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            return FeatureObjectResultModel<GetBinRecordByIdResponse>.Ok(new GetBinRecordByIdResponse
            {
                Id = record.Id,
                BinStart = record.BinRange.Start,
                BinEnd = record.BinRange.End,
                CardBrand = record.CardInfo.CardBrand,
                CardType = record.CardInfo.CardType,
                IssuingCountry = record.CardInfo.IssuingCountry,
                Region = record.CardInfo.Region
            });
        }
    }
}