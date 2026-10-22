using Payment.Api.Domains.PaymentLinks.BankCharge;

namespace Payment.Api.Domains.PaymentLinks.Features.Commands;

/// <summary>
/// Phase-1 3DS (İlke VI/VII): müşteri "3D ile öde" der. Link açılır (Pending→Opened) ve banka 3D gateway'ine
/// gidecek İMZALI form üretilir — PAN İÇERMEZ (kart alanlarını tarayıcı ekler, PG sunucusu görmez; SAQ A-EP).
/// Banka seçimi PAN'sız: BIN/on-us routing yapılamaz → maliyet sırasında ilk 3D-yetenekli banka
/// (`BankChargeRouter.SelectThreeDAdapter`). OkUrl/FailUrl = PG'nin 3d-callback ucu (banka buraya auto-POST eder).
/// </summary>
public static class Initiate3DPayment
{
    public record Command(string OpenToken, string CallbackUrl);

    public class Response
    {
        public string StoreName { get; set; } = string.Empty;
        public string OrderSummary { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string ActionUrl { get; set; } = string.Empty;
        public string BankCode { get; set; } = string.Empty;
        public List<KeyValuePair<string, string>> Fields { get; set; } = new();
    }

    [Transactional]
    public class Handler
    {
        public async Task<FeatureObjectResultModel<Response>> Handle(
            Command cmd, IDocumentSession session, BankChargeRouter router, CancellationToken ct)
        {
            var link = await session.Query<PaymentLink>().Where(x => x.OpenToken == cmd.OpenToken).FirstOrDefaultAsync(ct);
            if (link is null)
                return FeatureObjectResultModel<Response>.Error(new MessageItem { Code = PaymentLinkResourceConstants.LINK_NOT_FOUND });

            var opened = link.Open(DateTime.UtcNow);
            session.Store(link);
            if (!opened.IsSuccess)
                return FeatureObjectResultModel<Response>.Error(opened.Messages);

            // 3D-yetenekli banka (PAN yok → maliyet sırası). Yoksa 3DS açılamaz (NonSecure akışına düşülür).
            var adapter = router.SelectThreeDAdapter();
            if (adapter is not IThreeDCapableAdapter td)
                return FeatureObjectResultModel<Response>.Error(new MessageItem { Code = PaymentLinkResourceConstants.PROVIDER_UNAVAILABLE });

            var callback = cmd.CallbackUrl + "?bank=" + adapter.BankCode;
            var form = td.Build3DForm(link.OrderRef, link.Amount, link.Currency, callback, callback);

            return FeatureObjectResultModel<Response>.Ok(new Response
            {
                StoreName = link.StoreName,
                OrderSummary = link.OrderSummary,
                Amount = link.Amount,
                ExpiresAt = link.ExpiresAt,
                ActionUrl = form.ActionUrl,
                BankCode = adapter.BankCode,
                Fields = form.HiddenFields.ToList()
            });
        }
    }
}
