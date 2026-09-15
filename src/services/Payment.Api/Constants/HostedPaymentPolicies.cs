namespace Payment.Api.Constants;

// 041: Payment-özel authorization policy adları (Common.AuthorizationPolicies'e eklenmez — bu policy
// yalnız Payment BC'nin hosted-ödeme ucuna özgü, route'suz X-Api-Key tenant modeli).
public static class HostedPaymentPolicies
{
    /// <summary>Store→PG hosted ödeme başlatma: X-Api-Key şeması + authenticated; tenant merchant_id
    /// claim'inden (route'ta {merchantId} yok). Active statü kapısı slice içinde (fail-closed).</summary>
    public const string HostedPaymentApiKey = "hosted-payment-api-key";
}
