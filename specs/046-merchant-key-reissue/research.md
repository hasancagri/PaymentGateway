# Research — Merchant Key Yenileme

Tasarım kararları spec'te kilitli; burada yalnız kod-gerçeğinden çıkan teknik çözümler.

## Karar 1 — Eski key'i HER temsilde öldürme (FR-004)

- **Karar**: Yeni event `MerchantKeyReissued(MerchantId, MerchantKey)`; Identity + Payment.Api tüketir.
- **Gerekçe**: Bugün `MerchantCreated` key'i taşıyıp iki tüketiciyi besliyor (Identity client_secret
  `MerchantClientEventHandler`, Payment.Api hash `MerchantApiConsumers.StoreApiKey`). Aynı iki tüketici
  yeni event'i işleyip GÜNCELLER → eski secret/hash yerini yenisi alır → eski key 401.
- **Alternatif red**: `MerchantCreated`'ı yeniden yayınlamak — yanlış semantik ("created"), idempotent
  upsert'te yan etki riski.

## Karar 2 — Payment.Api hash REPLACE (yoksa eski hash yaşar)

- **Karar**: `Handle(MerchantKeyReissued)` eski `MerchantApiKeyReference`'i merchantId ile bulup
  KeyHash'i günceller (ya da sil+store). Doküman merchantId anahtarlı olmalı ki eski hash kalmasın.
- **Gerekçe**: `ApiKeyHash.Compute` sonucu doc id'siyse yeni hash EKLENİR, eski geçerli kalır → FR-003
  ihlali. Kritik implementasyon noktası (tasks'ta guard).

## Karar 3 — Teslim: store'a reveal URL döner (key değil)

- **Karar**: Reissue S2S yanıtı yeni `CredentialRevealLink` URL'sini + expiry döner; store bunu merchant
  admin'e gösterir; merchant açar, key'i bir kez görür. Önceki reveal linkleri `Kill()`.
- **Gerekçe**: URL capability-link, sır değil (045/078 emsali). Key sohbet/log/S2S body'sinde geçmez
  (FR-005). Onboarding'de reveal linki mail'le gidiyordu; reissue'de S2S yanıtı taşır (store-led).

## Karar 4 — Audit deseni yok → yeni salt-append doc

- **Karar**: `MerchantKeyReissueLog` (Marten doc; store-only, update yok). Merchant.Api'de AdminActionLog
  benzeri desen YOK; minimal record.
- **Gerekçe**: FR-007 salt-append; aggregate değil (invariant yok) → İLKE II log istisnası.

## Karar 5 — Tetik yetkisi

- **Karar**: Store S2S REST, ecommerce-onboarding m2m token (045 onboarding S2S grubu). Merchant key
  ile DEĞİL (chicken-egg). MerchantId body/route'ta.
- **Gerekçe**: Mevcut S2S auth deseni; yeni scope/istemci gerekmez. Rate-limit FR-012 = S1 borcu.
