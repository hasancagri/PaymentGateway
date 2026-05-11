// TODO: Task 9 will refactor WebhookDispatcher to use local MerchantSummary Marten document
// Cross-context references to MerchantManagementContext removed temporarily

namespace PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Features.Dispatchers;

public class WebhookDispatcher
{
    public Task Handle(PaymentApproved evt, IHttpClientFactory httpClientFactory,
        ILogger<WebhookDispatcher> logger, CancellationToken ct) =>
        SendWebhookAsync(evt.MerchantId, evt.TransactionId, evt.OrderId,
            isApproved: true, evt.ResultCode, message: null, evt.BankTransactionId,
            httpClientFactory, logger, ct);

    public Task Handle(PaymentDeclined evt, IHttpClientFactory httpClientFactory,
        ILogger<WebhookDispatcher> logger, CancellationToken ct) =>
        SendWebhookAsync(evt.MerchantId, evt.TransactionId, evt.OrderId,
            isApproved: false, evt.BankResponseCode, evt.BankMessage, bankTransactionId: null,
            httpClientFactory, logger, ct);

    public Task Handle(PaymentFailed evt, IHttpClientFactory httpClientFactory,
        ILogger<WebhookDispatcher> logger, CancellationToken ct) =>
        SendWebhookAsync(evt.MerchantId, evt.TransactionId, evt.OrderId,
            isApproved: false, resultCode: "99", evt.Reason, bankTransactionId: null,
            httpClientFactory, logger, ct);

    private async Task SendWebhookAsync(
        Guid merchantId, Guid transactionId, string orderId,
        bool isApproved, string resultCode, string? message, string? bankTransactionId,
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookDispatcher> logger, CancellationToken ct)
    {
        // TODO: Task 9 — look up merchant webhook URL from local MerchantSummary Marten document
        logger.LogWarning("WebhookDispatcher: merchant lookup not yet implemented (Task 9). MerchantId={MerchantId}", merchantId);
        await Task.CompletedTask;
    }
}