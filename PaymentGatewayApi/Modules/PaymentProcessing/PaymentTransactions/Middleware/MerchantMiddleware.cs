using PaymentGatewayApi.Modules.MerchantManagement.Merchants;
using PaymentGatewayApi.Modules.MerchantManagement.Merchants.Enums;
using PaymentGatewayApi.Modules.MerchantManagement.Merchants.ValueObjects;

namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Middleware;

public class MerchantMiddleware
{
    public static async Task<(HandlerContinuation, MerchantIdentity)> BeforeAsync(
        IHttpContextAccessor httpContextAccessor,
        MerchantManagementContext db,
        CancellationToken ct)
    {
        var apiKey = httpContextAccessor.HttpContext?.Request.Headers["X-Api-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(apiKey))
            throw new UnauthorizedAccessException("API key is required.");

        var keyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));
        var lookupKey = ApiKeyValue.FromHash(keyHash);

        var merchant = await db.Set<Merchant>()
            .Where(m => m.ApiKeys.Any(k =>
                k.KeyValue == lookupKey &&
                k.Status == ApiKeyStatus.Active &&
                (k.ExpiresAt == null || k.ExpiresAt > DateTime.UtcNow)))
            .FirstOrDefaultAsync(ct);

        if (merchant is null)
            throw new UnauthorizedAccessException("Invalid or expired API key.");

        if (merchant.Status != MerchantStatus.Active)
            throw new UnauthorizedAccessException("Merchant account is not active.");

        return (HandlerContinuation.Continue, new MerchantIdentity(
            merchant.Id,
            merchant.Name.Value
        ));
    }
}