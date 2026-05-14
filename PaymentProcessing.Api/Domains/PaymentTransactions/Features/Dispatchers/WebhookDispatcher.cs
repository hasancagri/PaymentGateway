namespace PaymentProcessing.Api.Domains.PaymentTransactions.Features.Dispatchers;

public class WebhookDispatcher
{
    public Task Handle(PaymentApproved evt, IQuerySession session, IHttpClientFactory httpClientFactory,
        ILogger<WebhookDispatcher> logger, CancellationToken ct) =>
        SendWebhookAsync(evt.MerchantId, evt.TransactionId, evt.OrderId,
            isApproved: true, evt.ResultCode, message: null, evt.BankTransactionId,
            session, httpClientFactory, logger, ct);

    public Task Handle(PaymentDeclined evt, IQuerySession session, IHttpClientFactory httpClientFactory,
        ILogger<WebhookDispatcher> logger, CancellationToken ct) =>
        SendWebhookAsync(evt.MerchantId, evt.TransactionId, evt.OrderId,
            isApproved: false, evt.BankResponseCode, evt.BankMessage, bankTransactionId: null,
            session, httpClientFactory, logger, ct);

    public Task Handle(PaymentFailed evt, IQuerySession session, IHttpClientFactory httpClientFactory,
        ILogger<WebhookDispatcher> logger, CancellationToken ct) =>
        SendWebhookAsync(evt.MerchantId, evt.TransactionId, evt.OrderId,
            isApproved: false, resultCode: "99", evt.Reason, bankTransactionId: null,
            session, httpClientFactory, logger, ct);

    private async Task SendWebhookAsync(
        Guid merchantId, Guid transactionId, string orderId,
        bool isApproved, string resultCode, string? message, string? bankTransactionId,
        IQuerySession session, IHttpClientFactory httpClientFactory,
        ILogger<WebhookDispatcher> logger, CancellationToken ct)
    {
        var merchant = await session.LoadAsync<MerchantSummary>(merchantId, ct);
        if (merchant is null)
        {
            logger.LogWarning("WebhookDispatcher: MerchantSummary not found. MerchantId={MerchantId}", merchantId);
            return;
        }

        if (!merchant.IsActive)
        {
            logger.LogWarning("WebhookDispatcher: Merchant is not active. MerchantId={MerchantId}", merchantId);
            return;
        }

        var payload = new
        {
            transactionId,
            orderId,
            isApproved,
            resultCode,
            message,
            bankTransactionId
        };

        try
        {
            var client = httpClientFactory.CreateClient();
            await client.PostAsJsonAsync(merchant.WebhookUrl, payload, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WebhookDispatcher: Failed to send webhook. MerchantId={MerchantId}, Url={Url}",
                merchantId, merchant.WebhookUrl);
        }
    }
}