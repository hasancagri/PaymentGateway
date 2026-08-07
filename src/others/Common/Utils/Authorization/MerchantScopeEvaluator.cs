namespace Common.Utils.Authorization;

// 012 (D7): tenant erişim kararının saf çekirdeği — host'suz birim testlerin hedefi.
// Karar tablosu (data-model §5): claim yok → izin; claim var + route yok → ret (fail-closed);
// claim var + route var → büyük/küçük harf duyarsız eşitlik.
public static class MerchantScopeEvaluator
{
    public static bool IsAllowed(string? merchantIdClaim, string? routeMerchantId)
    {
        // Claim'siz token = statik istemci (admin-ui, payment-agent) — mevcut davranış korunur.
        if (string.IsNullOrWhiteSpace(merchantIdClaim))
            return true;

        // Merchant token'ı yalnız kendi kimliğiyle adreslenen uca girer; route'ta merchantId
        // yoksa (liste, by-key, create) erişim kapalı.
        if (string.IsNullOrWhiteSpace(routeMerchantId))
            return false;

        // Guid yazımı farkları (büyük/küçük harf) eşleşmeyi bozmasın.
        return string.Equals(merchantIdClaim.Trim(), routeMerchantId.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}