namespace Common.Utils.Constants;

// 012: claim-tabanlı tenant policy'leri (scope policy'lerinin üstüne eklenir; adlar scope seti ile
// çakışmaz). G3 insan düzlemi aynı policy'leri yeniden kullanacak.
public static class AuthorizationPolicies
{
    /// <summary>
    /// Token'da merchant_id claim'i varsa route'daki {merchantId} ile birebir eşleşme zorunlu;
    /// route'ta değer yoksa RET (fail-closed). Claim'siz token'lar (admin-ui, payment-agent) geçer.
    /// </summary>
    public const string MerchantScoped = "merchant-scoped";

    /// <summary>
    /// Yalnız merchant_id claim'i TAŞIMAYAN (statik istemci) token'lar geçer — admin düzlemi uçları
    /// (ör. merchant status değiştirme; merchant kendini askıdan çıkaramaz).
    /// </summary>
    public const string AdminPlaneOnly = "admin-plane-only";

    /// <summary>
    /// 039: yapısal çekim/retrieve — X-Api-Key şeması ile kimliklenir (JWT değil), sonra MerchantScoped
    /// gibi merchant_id claim'i == route {merchantId} zorunlu. ECom Order.Api server-to-server yolu.
    /// </summary>
    public const string MerchantApiKey = "merchant-api-key";
}