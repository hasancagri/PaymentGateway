namespace Payment.Api.Domains.HostedPayments.Features.Commands;

/// <summary>
/// 041 US2/US3 (088 Phase-0 iskelet): hosted form dönüşünü işler. Uç kimliksiz değil — session per-session
/// secret <c>CallbackToken</c> ile bulunur (C1; bilinmeyen → SESSION_NOT_FOUND). Zaten terminal ise
/// store-bildirim aynı sonuçla tekrar publish edilir (idempotent, FR-008). Terminal geçişte store'a imzalı
/// bildirim [Transactional] outbox ile publish.
/// <para><b>088 teardown:</b> sağlayıcı form retrieve (sonuç teyidi) SÖKÜLDÜ. Terminal-republish + imzalı
/// store-callback akışı korunur; sağlayıcıdan sonuç teyidi Phase-1'de banka adapter'ı (İlke VII) ile
/// bağlanır (o ana dek non-terminal dönüş PROVIDER_UNAVAILABLE).</para>
/// </summary>
public static class CompleteHostedPayment
{
    private const string StatusSuccess = "Success";
    private const string StatusFailed = "Failed";

    public record CompleteHostedPaymentCommand(string CallbackToken, string ProviderToken);

    public class CompleteHostedPaymentResponse
    {
        public string PgPaymentRef { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Success / Failed
    }

    [Transactional]
    public class CompleteHostedPaymentCommandHandler
    {
        public async Task<FeatureObjectResultModel<CompleteHostedPaymentResponse>> Handle(
            CompleteHostedPaymentCommand cmd, IDocumentSession session, IMessageBus bus,
            ILogger<CompleteHostedPaymentCommandHandler> logger, CancellationToken ct)
        {
            // C1 secret-token kapısı: session yalnız geçerli CallbackToken ile bulunur.
            var hostedSession = await session.Query<HostedPaymentSession>()
                .FirstOrDefaultAsync(x => x.CallbackToken == cmd.CallbackToken, ct);
            if (hostedSession is null)
            {
                logger.LogWarning("Bilinmeyen callbackToken ile dönüş — işlenmedi.");
                return FeatureObjectResultModel<CompleteHostedPaymentResponse>.Error(new MessageItem
                { Code = HostedPaymentResourceConstants.SESSION_NOT_FOUND });
            }

            // Zaten terminal (FR-008): store-bildirim aynı sonuçla tekrar publish (idempotent).
            if (hostedSession.IsTerminal)
            {
                await PublishStoreCallback(hostedSession, bus);
                return Terminal(hostedSession);
            }

            // TODO Phase-1: banka adapter sonuç teyidi (eski sağlayıcı form retrieve) — session'ı
            //   MarkSucceeded/MarkFailed ile terminal'e taşır, sonra PublishStoreCallback. O ana dek
            //   non-terminal dönüş teyit edilemez → PROVIDER_UNAVAILABLE (session Pending kalır, retry'lenebilir).
            return FeatureObjectResultModel<CompleteHostedPaymentResponse>.Error(new MessageItem
            { Code = HostedPaymentResourceConstants.PROVIDER_UNAVAILABLE });
        }

        private static async Task PublishStoreCallback(HostedPaymentSession s, IMessageBus bus)
        {
            var status = s.Status == HostedPaymentStatus.Succeeded ? StatusSuccess : StatusFailed;
            await bus.PublishAsync(new StoreCallbackDelivery.Deliver(
                s.CallbackUrl, s.OrderRef, s.Id.ToString("N"), status, s.FailureReason));
        }

        private static FeatureObjectResultModel<CompleteHostedPaymentResponse> Terminal(HostedPaymentSession s)
            => FeatureObjectResultModel<CompleteHostedPaymentResponse>.Ok(new CompleteHostedPaymentResponse
            {
                PgPaymentRef = s.Id.ToString("N"),
                Status = s.Status == HostedPaymentStatus.Succeeded ? StatusSuccess : StatusFailed
            });
    }
}
