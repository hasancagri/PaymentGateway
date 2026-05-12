namespace MerchantManagement.Api.Domains.Merchants.Features.Commands;

public static class SuspendMerchant
{
    public class SuspendMerchantCommand
    {
        public required Guid MerchantId { get; set; }
        public required string Reason { get; set; }
    }

    public class SuspendMerchantCommandResponse
    {
    }

    [Transactional]
    public class SuspendMerchantHandler
    {
        public async Task<FeatureObjectResultModel<SuspendMerchantCommandResponse>> Handle(
            SuspendMerchantCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(cmd.MerchantId, ct);
            if (merchant is null)
                return FeatureObjectResultModel<SuspendMerchantCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Merchant),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = merchant.Suspend(cmd.Reason);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<SuspendMerchantCommandResponse>.Error(result.Messages!);

            session.Store(merchant);
            
            return FeatureObjectResultModel<SuspendMerchantCommandResponse>.Ok(new SuspendMerchantCommandResponse());
        }
    }
}