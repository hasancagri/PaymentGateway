# Dış Sözleşme: Onboarding Yüzeyi (015 — eklemeli değişim)

Bu feature dış sözleşmeyi yalnız EKLEMELİ değiştirir. Aşağıda **değişen** uçlar; değişmeyenler
"KORUNUR" ile işaretli. Kaldırılan alan/durum YOKTUR.

## MCP: `submit_registration` (KORUNUR + eklemeli)

**Girdi**: `descriptorUrl: string`, `externalRef?: string` — DEĞİŞMEZ.

**Çıktı** (`SubmitRegistrationResponse`) — alanlar DEĞİŞMEZ; doldurma davranışı eklemeli:

| Alan | Önce | Sonra (015) |
|------|------|-------------|
| `Status` | `"ChallengeRequired"` \| `"Pending"` | AYNI (string sözleşmesi korunur) |
| `RequestId` | yalnız `"Pending"`'de dolu | **`"ChallengeRequired"`'da da dolu** (AwaitingDomainControl talebinin Id'si) |
| `Token` / `ExpectedValue` / `PublishPath` | ChallengeRequired'da dolu | AYNI |
| `Message` | Türkçe metin | AYNI (durum + sıradaki adım) |

> ECommerce açısından: `RequestId` artık challenge aşamasında da döner → "benim sürecim" referansı.
> `Status` string'i değişmediğinden mevcut ECommerce mantığı kırılmaz.

## MCP: `registration_status` (KORUNUR + eklemeli)

**Girdi**: `domain: string` — DEĞİŞMEZ. (Sorgu domain ile; `RequestId` ile bir talebin durumu da
domain üzerinden bulunur.)

**Çıktı** (`RegistrationStatusResponse`):

| Alan | Önce | Sonra (015) |
|------|------|-------------|
| `Status` | `Pending`/`Approved`/`Rejected` | **`AwaitingDomainControl` de dönebilir** (enum adı) |
| `RequestId` | Guid? | AYNI |
| `Message` | (yok) | **YENİ** — Türkçe metin: güncel durum + sıradaki adım (on-demand "sürecim ne oldu?" yanıtı) |

> `AwaitingDomainControl` yeni bir enum değeri; mevcut değerler kaldırılmaz. ECommerce'e poll/
> durum-makinesi zorunluluğu getirilmez — istediğinde sorar, metin alır.

## MCP: `get_merchant` (KORUNUR)

DEĞİŞMEZ (id, ad, e-posta, durum). Tool açıklama metni gerekiyorsa güncellenir (davranış değil).

## HTTP: `POST merchants/activation/redeem` (KORUNUR)

**Girdi**: `{ activationToken: string }` — DEĞİŞMEZ.

**Çıktı**: `{ merchantId, merchantKey }` — DEĞİŞMEZ (MerchantKey bir kez döner).

**Yetki**: `merchant.write` + `AdminPlaneOnly` — DEĞİŞMEZ.

**İç değişim**: Redeem artık `ActivationTicket` yerine `Merchant`'ı `ActivationToken` ile bulur;
`Merchant.RedeemActivation` invariant'ı uygular. Dış davranış (tek-kullanım, TTL, `MerchantProvisioned`
yayını) AYNI.

## HTTP: RegisterRequest admin uçları (KORUNUR)

`GET /register-requests`, `GET /register-requests/{id}`, `POST /register-requests/{id}/approve`,
`POST /register-requests/{id}/reject` — girdi/çıktı/yetki (`AdminPlaneOnly`) DEĞİŞMEZ. Liste/detay
artık `AwaitingDomainControl` statüsünü de gösterebilir/filtreleyebilir (eklemeli).

## Integration event: `MerchantProvisioned` (KORUNUR)

`MerchantProvisioned(merchantId, merchantKey, status)` — sözleşme DEĞİŞMEZ; redeem'de yayınlanır.
Identity.Server tüketimi etkilenmez.

## Challenge yayın yolu (KORUNUR)

Aday, `/.well-known/merchant-challenge/{token}` yolunda `ExpectedValue`'yu yayınlar; gateway senkron
GET ile çeker. Yol + davranış DEĞİŞMEZ (token/expected artık RegisterRequest üstünde tutulur, aday
görünürlüğü aynı).

## descriptor.json (KORUNUR)

Aday `/.well-known/merchant-descriptor.json` beyanı (legalName, taxId, contactEmail, webhookUrl,
opsiyonel agent.a2aCardUrl) DEĞİŞMEZ. `MerchantDescriptor` VO doğrulaması aynı.