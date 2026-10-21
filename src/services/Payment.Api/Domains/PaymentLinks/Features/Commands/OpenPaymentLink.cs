using Payment.Api.Domains.PaymentLinks;

namespace Payment.Api.Domains.PaymentLinks.Features.Commands;

/// <summary>
/// 089 US2: Müşteri gizli açılış token'ıyla linki açar. Bilinmeyen/sahte token hiçbir linki açamaz
/// (fail-closed, FR-007 — İlke V). Başarılı açılışta link Pending→Opened geçer ve güven bağlamı
/// (mağaza adı + sipariş özeti + tutar + son geçerlilik) döner; uç bunu kart formundan ÖNCE render eder (FR-006).
/// Süresi geçmiş link lazy Expired'a döner ve açılmaz (FR-009).
/// </summary>
public static class OpenPaymentLink
{
    public record OpenPaymentLinkCommand(string OpenToken);

    public class OpenPaymentLinkResponse
    {
        public string StoreName { get; set; } = string.Empty;
        public string OrderSummary { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    [Transactional]
    public class OpenPaymentLinkCommandHandler
    {
        public async Task<FeatureObjectResultModel<OpenPaymentLinkResponse>> Handle(
            OpenPaymentLinkCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var link = await session.Query<PaymentLink>()
                .Where(x => x.OpenToken == cmd.OpenToken)
                .FirstOrDefaultAsync(ct);

            // Bilinmeyen token → hiçbir link açılmaz (fail-closed, FR-007).
            if (link is null)
                return FeatureObjectResultModel<OpenPaymentLinkResponse>.Error(new MessageItem
                { Code = PaymentLinkResourceConstants.LINK_NOT_FOUND });

            var opened = link.Open(DateTime.UtcNow);
            session.Store(link); // Opened geçişi ya da lazy Expired flip'i kalıcı olur.

            if (!opened.IsSuccess)
                return FeatureObjectResultModel<OpenPaymentLinkResponse>.Error(opened.Messages);

            return FeatureObjectResultModel<OpenPaymentLinkResponse>.Ok(new OpenPaymentLinkResponse
            {
                StoreName = link.StoreName,
                OrderSummary = link.OrderSummary,
                Amount = link.Amount,
                ExpiresAt = link.ExpiresAt
            });
        }
    }
}
