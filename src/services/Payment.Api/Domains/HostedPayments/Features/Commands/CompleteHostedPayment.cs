using Iyz = Payment.Api.Utils;
using Payment.Api.Options;

namespace Payment.Api.Domains.HostedPayments.Features.Commands;

/// <summary>
/// 041 US2/US3: iyzico hosted form dönüşünü işler. Uç kimliksiz değil — session per-session secret
/// <c>CallbackToken</c> ile bulunur (C1; bilinmeyen → SESSION_NOT_FOUND). Sonuç iyzico'dan BAĞIMSIZ CF
/// retrieve ile TEYİT edilir (kaynak-of-truth; sahte gövde düşer). Terminal → tek-yön (FR-008); zaten
/// terminal ise retrieve atlanır, store-bildirim aynı sonuçla tekrar publish edilir (idempotent). Terminal
/// geçişte store'a imzalı bildirim [Transactional] outbox ile publish (FR-006/FR-009).
/// </summary>
public static class CompleteHostedPayment
{
    private const string SuccessPaymentStatus = "SUCCESS";
    private const string StatusSuccess = "Success";
    private const string StatusFailed = "Failed";

    public record CompleteHostedPaymentCommand(string CallbackToken, string IyzicoToken);

    public class CompleteHostedPaymentResponse
    {
        public string PgPaymentRef { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Success / Failed
    }

    // --- iyzico Checkout Form retrieve wire (slice-sahipli; camelCase JSON) ---

    public class RetrieveCheckoutFormRequest
    {
        public string Locale { get; set; } = string.Empty;
        public string ConversationId { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }

    public class RetrieveCheckoutFormResult : Iyz.ProviderResourceV2
    {
        public string PaymentStatus { get; set; } = string.Empty;
        public string PaymentId { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string PaidPrice { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public string FraudStatus { get; set; } = string.Empty;
    }

    [Transactional]
    public class CompleteHostedPaymentCommandHandler
    {
        public async Task<FeatureObjectResultModel<CompleteHostedPaymentResponse>> Handle(
            CompleteHostedPaymentCommand cmd, IDocumentSession session, IMessageBus bus,
            Iyz.ProviderOptions providerOptions, IyzicoRequestOptions requestOptions,
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

            // Zaten terminal (FR-008): retrieve ATLA, store-bildirim aynı sonuçla tekrar publish (idempotent).
            if (hostedSession.IsTerminal)
            {
                await PublishStoreCallback(hostedSession, bus);
                return Terminal(hostedSession);
            }

            RetrieveCheckoutFormResult iyzicoResult;
            try
            {
                var conversationId = hostedSession.Id.ToString("N");
                var request = new RetrieveCheckoutFormRequest
                {
                    Locale = requestOptions.Locale,
                    ConversationId = conversationId,
                    Token = cmd.IyzicoToken
                };
                var uri = providerOptions.BaseUrl + requestOptions.CheckoutFormRetrievePath;
                var headers = Iyz.ProviderResourceV2.GetHttpHeadersWithRequestBody(request, uri, providerOptions, conversationId);
                iyzicoResult = await Iyz.RestHttpClientV2.Create().PostAsync<RetrieveCheckoutFormResult>(uri, headers, request);
            }
            catch
            {
                // Sağlayıcı erişilemez → session Pending kalır, dönüş yeniden denenebilir (edge case).
                return FeatureObjectResultModel<CompleteHostedPaymentResponse>.Error(new MessageItem
                { Code = HostedPaymentResourceConstants.PROVIDER_UNAVAILABLE });
            }

            var succeeded = iyzicoResult is not null &&
                iyzicoResult.Status == requestOptions.SuccessStatus &&
                string.Equals(iyzicoResult.PaymentStatus, SuccessPaymentStatus, StringComparison.OrdinalIgnoreCase);

            var transition = succeeded
                ? hostedSession.MarkSucceeded(iyzicoResult!.PaymentId)
                : hostedSession.MarkFailed(iyzicoResult?.ErrorCode ?? iyzicoResult?.PaymentStatus ?? StatusFailed);
            if (!transition.IsSuccess)
                return FeatureObjectResultModel<CompleteHostedPaymentResponse>.Error(transition.Messages);
            session.Update(hostedSession);

            await PublishStoreCallback(hostedSession, bus); // FR-006 imzalı bildirim (outbox → DB commit'te gider)
            return Terminal(hostedSession);
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
