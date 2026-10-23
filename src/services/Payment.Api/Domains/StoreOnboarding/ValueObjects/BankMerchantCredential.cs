namespace Payment.Api.Domains.StoreOnboarding.ValueObjects;

/// <summary>
/// 091 (İlke VI, anayasa v3.1.0): Store'un bir bankadaki hesabını temsil eden banka credential'ı — SIR +
/// TRANSIENT. Aggregate'e GİRMEZ; banka→PG webhook gövdesinden üretilir, IBankCredentialStore'a (at-rest
/// şifreli) yazılır, oradan yalnız çekim yolu okur. Banka başına doluluk değişir (adapter normalize eder).
/// ToString maskeli — sır ASLA log/trace/DTO/webhook-dönüşünde (İlke VI). VO: record + private ctor + Create.
/// </summary>
public sealed record BankMerchantCredential
{
    public string ShopCode { get; }
    public string UserCode { get; }
    public string UserPass { get; }
    public string? SecretKey { get; }
    public string? SubscriptionKey { get; }

    private BankMerchantCredential(string shopCode, string userCode, string userPass, string? secretKey, string? subscriptionKey)
    {
        ShopCode = shopCode;
        UserCode = userCode;
        UserPass = userPass;
        SecretKey = secretKey;
        SubscriptionKey = subscriptionKey;
    }

    public static BankMerchantCredential Create(string shopCode, string userCode, string userPass,
        string? secretKey = null, string? subscriptionKey = null)
        => new(shopCode, userCode, userPass, secretKey, subscriptionKey);

    // Sır maskesi — yanlışlıkla log/trace'e düşse bile credential sızmaz (İlke VI).
    public override string ToString() => $"BankMerchantCredential(ShopCode={ShopCode}, ***)";
}
