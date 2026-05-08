// TODO: Task 9 will refactor MerchantMiddleware to use local MerchantSummary Marten document
// Cross-context references to MerchantManagementContext removed temporarily

namespace PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Middleware;

public class MerchantMiddleware
{
    public static Task<(HandlerContinuation, MerchantIdentity)> BeforeAsync(
        IHttpContextAccessor httpContextAccessor,
        CancellationToken ct)
    {
        var apiKey = httpContextAccessor.HttpContext?.Request.Headers["X-Api-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(apiKey))
            throw new UnauthorizedAccessException("API key is required.");

        // TODO: Task 9 — look up merchant from local MerchantSummary Marten document
        throw new NotImplementedException("MerchantMiddleware will be implemented in Task 9 using local MerchantSummary read model.");
    }
}