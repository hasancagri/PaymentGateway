namespace PaymentProcessing.Api.PaymentProcessing.PaymentTransactions.Middleware;

public class MerchantMiddleware
{
    public static (HandlerContinuation, MerchantIdentity) Before(
        IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;
        var merchantIdClaim = user?.FindFirst("merchant_id")?.Value;
        var merchantNameClaim = user?.FindFirst("merchant_name")?.Value;

        if (string.IsNullOrEmpty(merchantIdClaim))
            throw new UnauthorizedAccessException("Merchant context missing from token.");

        return (HandlerContinuation.Continue, new MerchantIdentity(
            Guid.Parse(merchantIdClaim),
            merchantNameClaim ?? string.Empty));
    }
}