using Payment.Api.Domains.PaymentLinks;

namespace Payment.Api.Domains.PaymentLinks.Features.Commands;

/// <summary>
/// 089 US3: Store bir ödeme linkini iptal eder (PgPaymentRef ile). Aktif (Pending/Opened) link Cancelled
/// terminal olur; terminal link'e iptal TERMINAL_VIOLATION (tek-yön, FR-008). İptal sonrası açma/ödeme reddedilir.
/// </summary>
public static class CancelPaymentLink
{
    public record CancelPaymentLinkCommand(string PgPaymentRef);

    [Transactional]
    public class CancelPaymentLinkCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            CancelPaymentLinkCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (!Guid.TryParse(cmd.PgPaymentRef, out var id))
                return FeatureResultModel.Error(new MessageItem { Code = PaymentLinkResourceConstants.LINK_NOT_FOUND });

            var link = await session.LoadAsync<PaymentLink>(id, ct);
            if (link is null)
                return FeatureResultModel.Error(new MessageItem { Code = PaymentLinkResourceConstants.LINK_NOT_FOUND });

            var cancelled = link.Cancel();
            if (!cancelled.IsSuccess)
                return FeatureResultModel.Error(cancelled.Messages);

            session.Store(link);
            return FeatureResultModel.Ok();
        }
    }
}
