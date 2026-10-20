namespace Payment.Api.Domains.HostedPayments.Features.Commands;

/// <summary>
/// 041 US1 (088 Phase-0 iskelet): Store için hosted ödeme başlatır. Kart bilgisi store/PG'ye girmez;
/// müşteri hosted ödeme sayfasında öder. Yanıt: HostedUrl + PgPaymentRef. Yalnız TRY (FR-012), tutar &gt; 0.
/// <para><b>088 teardown:</b> sağlayıcı form initialize çağrısı + merchant Active-gate (eski statü referansı)
/// SÖKÜLDÜ. Akış kavramı (store başlatma + alan doğrulama) korunur; sağlayıcı entegrasyonu Phase-1'de
/// banka PaymentLink adapter'ı (İlke VII port/adapter) ile bağlanır.</para>
/// </summary>
public static class InitiateHostedPayment
{
    private const string TryCurrency = "TRY";

    public record InitiateHostedPaymentCommand(
        Guid MerchantId, decimal Amount, string Currency, string OrderRef, string CallbackUrl);

    public class InitiateHostedPaymentResponse
    {
        public string HostedUrl { get; set; } = string.Empty;
        public string PgPaymentRef { get; set; } = string.Empty;
    }

    [Transactional]
    public class InitiateHostedPaymentCommandHandler
    {
        public Task<FeatureObjectResultModel<InitiateHostedPaymentResponse>> Handle(
            InitiateHostedPaymentCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            // Alan doğrulama: yalnız TRY (FR-012); tutar > 0 + zorunlu alanlar.
            if (!string.Equals(cmd.Currency, TryCurrency, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(FeatureObjectResultModel<InitiateHostedPaymentResponse>.Error(new MessageItem
                { Code = HostedPaymentResourceConstants.UNSUPPORTED_CURRENCY }));
            if (cmd.Amount <= 0 || string.IsNullOrWhiteSpace(cmd.OrderRef) || string.IsNullOrWhiteSpace(cmd.CallbackUrl))
                return Task.FromResult(FeatureObjectResultModel<InitiateHostedPaymentResponse>.Error(new MessageItem
                { Code = HostedPaymentResourceConstants.INVALID_REQUEST }));

            // TODO Phase-1: merchant Active-gate (eski MerchantStatusReference; fail-closed, İlke V) —
            //   statü kaynağı merchant.lifecycle fanout 088'de söküldü, yeni kaynak Phase-1'de bağlanır.
            // TODO Phase-1: PaymentLink adapter — HostedPaymentSession.Start + banka hosted sayfa/link
            //   üretimi (İlke VII port/adapter) burada olacak; sonuç AttachCheckoutForm ile iliştirilir.
            return Task.FromResult(FeatureObjectResultModel<InitiateHostedPaymentResponse>.Error(new MessageItem
            { Code = HostedPaymentResourceConstants.PROVIDER_UNAVAILABLE }));
        }
    }
}
