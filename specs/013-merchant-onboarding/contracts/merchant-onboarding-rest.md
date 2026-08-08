# Contract — REST Endpoints (Merchant.Api) + Aktivasyon

Minimal API, `*EndpointExtension`, handler `IMessageBus.InvokeAsync`. Auth: mevcut scope +
policy modeli (`merchant.read/write`, `MerchantScoped`, `AdminPlaneOnly`).

## Admin düzlemi — RegisterRequest yönetimi (AdminPlaneOnly)

Admin BFF (admin-ui client, merchant_id claim'i YOK) tüketir. "Merchant Talepleri" sayfası.

| Method | Route | Policy | Açıklama |
|--------|-------|--------|----------|
| GET | `register-requests?status=Pending` | `merchant.read` + `AdminPlaneOnly` | Bekleyen talepleri listele (domain, descriptor özeti, challenge durumu) |
| GET | `register-requests/{id}` | `merchant.read` + `AdminPlaneOnly` | Talep ayrıntısı |
| POST | `register-requests/{id}/approve` | `merchant.write` + `AdminPlaneOnly` | Onay → merchant oluştur (key üret) + ActivationTicket + aktivasyon maili. Gövde: `{ note? }` |
| POST | `register-requests/{id}/reject` | `merchant.write` + `AdminPlaneOnly` | Ret → Rejected. Gövde: `{ note? }` |

**Approve akışı** (handler, `[Transactional]`): RegisterRequest.Approve → mevcut CreateMerchant
(statü **Provisioning**, MerchantKey üret) → ActivationTicket.Issue → aktivasyon maili
(IMailSender, link = Identity aktivasyon sayfası + token) → OnboardingNotification kaydı →
outbox. **MerchantProvisioned burada YAYINLANMAZ** (aktivasyonda yayınlanır — D1/D4).

## Merchant düzlemi — self-service (MerchantScoped, Provisioning+)

Merchant kendi token'ıyla (merchant_id claim'i route ile eşleşir).

| Method | Route | Policy | Açıklama |
|--------|-------|--------|----------|
| GET | `merchants/{merchantId}` | `merchant.read` + `MerchantScoped` | mevcut (GetMerchant) |
| PUT | `merchants/{merchantId}/return-url` | `merchant.write` + `MerchantScoped` | ReturnUrl set/update (HTTPS). Sonra `TryActivate`. Gövde: `{ returnUrl }` |
| * | `merchants/{merchantId}/settlement-accounts/**` | mevcut | Settlement (koşul #1). İlk hesap → `MarkSettlementAccountPresent` + `TryActivate` |

`externalRef`: merchant'a dönük kayıt uçlarında opsiyonel alan olarak kabul/sakla/aynen dön
(FR-018). 013'te asıl tüketici charge (G5) yok; alan + saklama kurulur.

## Aktivasyon — key redeem (Identity.Server sayfası → Merchant.Api)

Identity aktivasyon Razor sayfası, formu Merchant.Api'ye **senkron** POST eder (sanksiyonlu
cross-servis çağrı — Complexity Tracking).

| Method | Route | Policy | Açıklama |
|--------|-------|--------|----------|
| POST | `merchants/activation/redeem` | iç/servis-arası (Identity client) | Gövde: `{ activationToken }`. Bilet doğrula (tek-kullanım, TTL) → `Merchant.Provision()` → `MerchantProvisioned` publish → yanıtta `{ merchantId, merchantKey }` **bir kez** |

- İkinci redeem / süre dolmuş → Result hata (key yeniden dönmez, FR-009). Identity sayfası
  bu hatayı gösterir; admin yeni bilet tetikleyebilir.
- MerchantKey yalnız bu yanıtta ve Identity sayfasının tek render'ında görünür; başka kanala
  çıkmaz.

## Identity.Server aktivasyon sayfası (Razor Pages, YENİ)

| Route | Açıklama |
|-------|----------|
| `GET /activation?token={activationToken}` | Formu göster (token gizli alan) |
| `POST /activation` | Merchant.Api `redeem` çağır → MerchantKey'i **bir kez** göster; kopyala uyarısı ("bir daha gösterilmez") |

`IgnoreHTTPSErrors` dev cert (E2E notu). Sayfa key custody tutmaz; yalnız redeem yanıtını render.