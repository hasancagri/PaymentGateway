using Marten;

namespace PaymentProcessing.Api.Modules.PaymentProcessing.BinRecords.Features.Queries;

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
        public string CardProductType { get; set; }
        public string BinCountry { get; set; }
        public string BinRegion { get; set; }
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
                BinStart = record.BinStart,
                BinEnd = record.BinEnd,
                CardBrand = record.CardBrand,
                CardProductType = record.CardProductType,
                BinCountry = record.BinCountry,
                BinRegion = record.BinRegion
            });
        }
    }
}