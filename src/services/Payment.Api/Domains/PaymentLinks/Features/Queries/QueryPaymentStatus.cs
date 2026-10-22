using Payment.Api.Domains.PaymentLinks.BankCharge;

namespace Payment.Api.Domains.PaymentLinks.Features.Queries;

/// <summary>
/// Store ödeme durumunu sorgular — link'in onaylı çekiminin bankasına `QueryStatusAsync(bankOrderId)` (banka
/// otorite). PG'nin link durumu + bankanın güncel hali döner. Salt-okur. (in-doubt sonrası store'un kesin
/// cevabı buradan.)
/// </summary>
public static class QueryPaymentStatus
{
    public record Query(string PgPaymentRef);

    public class Response
    {
        public string LinkStatus { get; set; } = string.Empty;
        public bool BankApproved { get; set; }
        public string BankRawCode { get; set; } = string.Empty;
    }

    public class QueryPaymentStatusHandler
    {
        public async Task<FeatureObjectResultModel<Response>> Handle(
            Query query, IDocumentSession session, IEnumerable<IBankPaymentAdapter> adapters, CancellationToken ct)
        {
            if (!Guid.TryParse(query.PgPaymentRef, out var id))
                return FeatureObjectResultModel<Response>.Error(new MessageItem { Code = PaymentLinkResourceConstants.LINK_NOT_FOUND });

            var link = await session.LoadAsync<PaymentLink>(id, ct);
            if (link is null)
                return FeatureObjectResultModel<Response>.Error(new MessageItem { Code = PaymentLinkResourceConstants.LINK_NOT_FOUND });

            var response = new Response { LinkStatus = link.Status.ToString() };

            var approved = link.Attempts.FirstOrDefault(a => a.IsApproved && !string.IsNullOrEmpty(a.BankOrderId));
            var adapter = approved is null ? null : adapters.FirstOrDefault(a => a.BankCode == approved.BankCode);
            if (approved is not null && adapter is not null)
            {
                var r = await adapter.QueryStatusAsync(approved.BankOrderId!, ct);
                response.BankApproved = r.Data!.IsApproved;
                response.BankRawCode = r.Data!.RawCode;
            }

            return FeatureObjectResultModel<Response>.Ok(response);
        }
    }
}
