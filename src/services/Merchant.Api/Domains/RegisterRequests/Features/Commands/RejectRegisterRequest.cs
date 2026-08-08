namespace Merchant.Api.Domains.RegisterRequests.Features.Commands;

/// <summary>US2 — admin reddi: talep Rejected olur, merchant OLUŞMAZ. O domainden yeni başvuru yapılabilir (FR-008).</summary>
public static class RejectRegisterRequest
{
    public record RejectRegisterRequestCommand(Guid RequestId, string? Note = null);

    public class RejectRegisterRequestResponse
    {
        public Guid RequestId { get; set; }
    }

    [Transactional]
    public class RejectRegisterRequestCommandHandler
    {
        public async Task<FeatureObjectResultModel<RejectRegisterRequestResponse>> Handle(
            RejectRegisterRequestCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var request = await session.LoadAsync<RegisterRequest>(cmd.RequestId, ct);
            if (request is null)
                return FeatureObjectResultModel<RejectRegisterRequestResponse>.NotFound();

            var reject = request.Reject(cmd.Note);
            if (!reject.IsSuccess)
                return FeatureObjectResultModel<RejectRegisterRequestResponse>.Error(reject.Messages);

            session.Update(request);

            return FeatureObjectResultModel<RejectRegisterRequestResponse>.Ok(new RejectRegisterRequestResponse
            {
                RequestId = request.Id
            });
        }
    }
}
