# Quickstart — Reissue doğrulama

Ön koşul: Aspire açık (Merchant/Identity/Payment), onboarding'i tamam bir Active merchant.

1. **Tetikle**: store S2S `POST /onboarding/reissue { merchantId }` → 200 + `revealUrl`.
2. **Eski key ölü**: eski MerchantKey ile hosted ödeme başlat → 401 (FR-003).
3. **Yeni key çalışır**: reveal URL'yi aç → yeni key'i bir kez oku → onunla hosted ödeme başlat → geçer.
4. **Reveal tek-kullanımlık**: URL'yi tekrar aç → nötr sayfa (FR-006).
5. **Denetim**: merchant reissue geçmişi → 1 kayıt (zaman + tetikleyen + neden) (FR-007).
6. **In-flight**: reissue'den önce üretilmiş hosted link'i tamamla → ödeme geçer (SC-003).

Saf domain testleri (İLKE VI, test-first): `Merchant.ReissueKey` (Active şartı, yeni key üretimi),
`CredentialRevealLink` Consume/Kill. Banka/HTTP çağrısı test edilmez (proje kuralı).
