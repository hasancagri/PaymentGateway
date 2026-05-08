using PaymentGatewayApi.Modules.MerchantManagement.Merchants;

namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Features.Dispatchers;

public class WebhookDispatcher
{
    public Task Handle(PaymentApproved evt, MerchantManagementContext db, IHttpClientFactory httpClientFactory,
        ILogger<WebhookDispatcher> logger, CancellationToken ct) =>
        SendWebhookAsync(evt.MerchantId, evt.TransactionId, evt.OrderId,
            isApproved: true, evt.ResultCode, message: null, evt.BankTransactionId,
            db, httpClientFactory, logger, ct);

    public Task Handle(PaymentDeclined evt, MerchantManagementContext db, IHttpClientFactory httpClientFactory,
        ILogger<WebhookDispatcher> logger, CancellationToken ct) =>
        SendWebhookAsync(evt.MerchantId, evt.TransactionId, evt.OrderId,
            isApproved: false, evt.BankResponseCode, evt.BankMessage, bankTransactionId: null,
            db, httpClientFactory, logger, ct);

    public Task Handle(PaymentFailed evt, MerchantManagementContext db, IHttpClientFactory httpClientFactory,
        ILogger<WebhookDispatcher> logger, CancellationToken ct) =>
        SendWebhookAsync(evt.MerchantId, evt.TransactionId, evt.OrderId,
            isApproved: false, resultCode: "99", evt.Reason, bankTransactionId: null,
            db, httpClientFactory, logger, ct);

    private async Task SendWebhookAsync(
        Guid merchantId, Guid transactionId, string orderId,
        bool isApproved, string resultCode, string? message, string? bankTransactionId,
        MerchantManagementContext db, IHttpClientFactory httpClientFactory,
        ILogger<WebhookDispatcher> logger, CancellationToken ct)
    {
        var merchant = await db.Set<Merchant>().FirstOrDefaultAsync(m => m.Id == merchantId, ct);
        if (merchant is null)
        {
            logger.LogError("Merchant {MerchantId} not found for webhook delivery", merchantId);
            return;
        }

        var payload = new { transactionId, orderId, isApproved, resultCode, message, bankTransactionId };

        var client = httpClientFactory.CreateClient("webhook");
        var response = await client.PostAsJsonAsync(merchant.WebhookUrl.Value, payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Webhook delivery failed for merchant {MerchantId}, status {Status}",
                merchantId, (int)response.StatusCode);
            if ((int)response.StatusCode < 500)
                return;
            response.EnsureSuccessStatusCode();
        }
    }
}