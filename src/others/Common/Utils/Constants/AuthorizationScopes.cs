namespace Common.Utils.Constants;

// 011: gateway scope seti (Identity.Server Config.ScopeResources ile birebir).
// Kural: GET → <bc>.read, durum değiştiren → <bc>.write. G2/G5 genişlemesi (cards.write, charge) buraya.
public static class AuthorizationScopes
{
    // payment.api
    public const string PaymentRead = "payment.read";
    public const string PaymentWrite = "payment.write";
    // 017: kart vault capability scope — Active merchant token'ına yalnız vault için verilir
    // (payment.write DEĞİL → /mcp + /pos-accounts merchant'a kapalı). Charge fail-closed.
    public const string CardsWrite = "cards.write";

    // merchant.api
    public const string MerchantRead = "merchant.read";
    public const string MerchantWrite = "merchant.write";

    // commission.api
    public const string CommissionRead = "commission.read";
    public const string CommissionWrite = "commission.write";
}