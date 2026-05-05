using PaymentGatewayApi.Modules.MerchantManagement.Merchants;

namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Features.Commands;

public static class WebhookQueued
{
    public record WebhookQueuedMessage(
        Guid MerchantId,
        Guid TransactionId,
        string OrderId,
        bool IsApproved,
        string ResultCode,
        string? Message,
        string? BankTransactionId
    );

    public class WebhookQueuedHandler
    {
        public async Task Handle(
            WebhookQueuedMessage msg,
            MerchantManagementContext db,
            IHttpClientFactory httpClientFactory,
            ILogger<WebhookQueuedHandler> logger,
            CancellationToken ct)
        {
            var merchant = await db.Set<Merchant>()
                .FirstOrDefaultAsync(m => m.Id == msg.MerchantId, ct);

            if (merchant is null)
            {
                logger.LogError("Merchant {MerchantId} not found for webhook delivery", msg.MerchantId);
                return;
            }

            var payload = new
            {
                transactionId = msg.TransactionId,
                orderId = msg.OrderId,
                isApproved = msg.IsApproved,
                resultCode = msg.ResultCode,
                message = msg.Message,
                bankTransactionId = msg.BankTransactionId
            };

            var client = httpClientFactory.CreateClient("webhook");
            var response = await client.PostAsJsonAsync(merchant.WebhookUrl.Value, payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Webhook delivery failed for merchant {MerchantId}, status {Status}",
                    msg.MerchantId, (int)response.StatusCode);
                if ((int)response.StatusCode < 500)
                    return;
                response.EnsureSuccessStatusCode();
            }
        }
    }
}