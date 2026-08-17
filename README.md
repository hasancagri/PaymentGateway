# PaymentGateway (DropShop)

Dropship modelli e-ticaretin **iyzico** tabanlı ödeme altyapısı. Her mikroservis bir **Bounded Context**;
Vertical Slice + CQRS, zengin aggregate'ler, Result pattern. Altyapı: **Aspire** (orkestrasyon),
**Marten** (Postgres document store), **Wolverine** (in-process bus + RabbitMQ), **YARP** (reverse proxy).

> **021-022 pivotu**: sistem iyzico ödeme kanalına döndü. Eski CP.VPOS sanal POS, BankRouter/PosAccount,
> BinCard, Reference.Api, kart-vault (017), A2A ödeme oturumu/MCP (007) ve tüm eski BC feature'ları SÖKÜLDÜ.
> Üç canlı BC: **Payment**, **Merchant**, **Commission**. Güncel mimari kararların tek kaynağı `CLAUDE.md`.

## Komutlar

```bash
dotnet build                                             # tüm çözüm (PaymentGateway.slnx)
dotnet run --project src/aspire/AppHost/AppHost.csproj   # sistemi Aspire ile başlat (Postgres + RabbitMQ)
dotnet test tests/Payment.Api.Tests                      # Payment saf domain birim testleri (29)
dotnet test tests/Merchant.Api.Tests                     # Merchant saf domain birim testleri (47)
dotnet test tests/Commission.Api.Tests                   # Commission saf domain birim testleri (31)
```

Sistemi her zaman AppHost üzerinden başlatın; servisler bağlantı dizelerini Aspire'dan alır.
Central Package Management açık — sürümler yalnız `Directory.Packages.props`'ta (022'de CP.VPOS/Iyzipay
adaları silindi). iyzico sandbox key/secret **user-secrets**'tan gelir (`IyzicoProviderSettings`, git'e girmez).

## Yapı

```text
src/
├── aspire/           AppHost + ServiceDefaults (orkestrasyon, service discovery)
├── agents/
│   ├── Payment.Agent  A2A host + LLM router (007 kalıntısı) — BC değil; MCP skill'leri 022'de ÖLÜ
│   └── Merchant.Agent A2A host — BC değil; register/komisyon skill'leri 022'de ÖLÜ (proje derlenir)
├── services/
│   ├── Payment.Api    Ödeme BC (iyzico: kart saklama/çekim/taksit; transport engine Utils/, 037)
│   ├── Merchant.Api   Merchant BC (onboarding + merchant CRUD/statü; = gateway müşterisi SİTE)
│   ├── Commission.Api Komisyon BC (CommissionPolicy: iyzico maliyeti + marj, 024)
│   └── gateway/Gateway YARP reverse proxy (service discovery ile; auth kabuk — grant ayrımı ertelendi)
├── ui/Admin          Razor Pages yönetim BFF (makine token'ıyla API çağırır; çoğu ekran ölü)
└── others/           Common (domain base, Result, auth, tenant enforcement) + Shared (integration event)
                      + Identity.Server (OpenIddict M2M IdP, 011/012) + Mail.Worker (RabbitMQ→SMTP, 016)
                      + Excel.Mcp (generic MCP) + SharedKernel
```

Bir feature = bir static class (record command/query + Response + Handler + endpoint). Handler'lar
`[Transactional]` + `IDocumentSession` (repository yok); sonuçlar `FeatureObjectResultModel<T>` /
`ResultDomain` (exception değil). Aggregate metotları yalnız handler'dan çağrılır; iyzico wire tipleri
BC sınırını geçmez.

## Bounded Context'ler

| BC | Sorumluluk |
|----|-----------|
| **Payment** | **iyzico** ödeme kanalı. `StoredCard` (iyzico Saklı Kart tokenizasyon, Model A — PAN gateway'de saklanmaz) + `Payment` (kayıtlı kartla NonSecure çekim) aggregate'leri; kart-saklama/çekim/taksit-sorgu slice'ları. |
| **Merchant** | Merchant onboarding (agentik başvuru + insan onayı + kademeli yetki) ve merchant CRUD/statü. **Merchant = gateway müşterisi SİTE** (ör. ECommerce) — pazaryeri/split DEĞİL. |
| **Commission** | `CommissionPolicy` (024): merchant komisyonu = **iyzico maliyeti + marj** (yüzde + sabit). Efektif komisyon hesabı; iyzico maliyeti işlem-sonrası rapordan beslenir (ileride). |

Altyapı (BC değil): `Identity.Server` (OpenIddict IdP), `Gateway` (YARP), `Mail.Worker` (016 — MCP DEĞİL),
`Excel.Mcp`, `Payment.Agent`/`Merchant.Agent` (A2A host'ları; skill'ler 022'de ölü), Admin BFF. Dev'de `Mailpit`.

## Payment BC — iyzico ödeme + transport (037)

### Aggregate'ler + slice'lar

- **`StoredCard`** — iyzico Saklı Kart tokenizasyon. **Model A**: PAN gateway'de saklanmaz; iyzico'ya iletilir,
  dönen `cardUserKey`+`cardToken` (opak) saklanır. CVC sözleşmede yok. Slice'lar: **`TokenizeCard`** (sakla →
  yalnız opak token döner), **`RevokeCard`** (iyzico'dan sil best-effort + yerel soft iptal, fail-open).
- **`Payment`** — kayıtlı kartla NonSecure çekim. Slice'lar: **`ChargePayment`** (vault token → StoredCard
  kiracı+Active kontrolü → iyzico çekim → başarıda `Payment` + `PaymentChargedEvent`, başarısızda `Failed`),
  **`InstallmentOptions`** (BIN + tutar → iyzico taksit seçenekleri; ödeme öncesi, oturum açmaz).

### API

| Metod | Yol | Slice | Yetki |
|-------|-----|-------|-------|
| `POST` | `…/merchants/{merchantId}/vault/cards` | TokenizeCard | `cards.write` + `MerchantScoped` |
| `DELETE` | `…/vault/cards/{token}` | RevokeCard | `cards.write` + `MerchantScoped` |
| `POST` | `…/merchants/{merchantId}/payments` | ChargePayment | `payment.charge` + `MerchantScoped` |
| `POST` | `…/payments/installment-options` | InstallmentOptions | `payment.charge` + `MerchantScoped` |

(Tümü `api/v{version}/` altında; kart-saklama/çekim yalnız **Active** merchant token'ında.)

### iyzico wire yapısı (037 — `Iyzico.Provider` SDK söküldü)

Paylaşılan `Iyzico.Provider` SDK'sı **silindi**. Yeni yapı:

- **Wire request/response tipleri kullanan slice'ın İÇİNDE nested** — base tip yok, düz camelCase JSON POCO;
  yanıtlar `Payment.Api.Utils.ProviderResourceV2`'den türer. Slice'ı açan iyzico çağrısını (istek + endpoint +
  yanıt parse) orada görür. Süreç netliği için wire feature'a gömülü; kod tekrarı **bilinçli** kabul.
- **Transport engine** (5 dosya: `RestHttpClientV2` / `ProviderResourceV2` / `HashGeneratorV2` /
  `ProviderConstants` / `ProviderOptions`) → `src/services/Payment.Api/Utils/` **tek kopya** (ns
  `Payment.Api.Utils`). V2 akışı JSON gövde + HMAC-SHA256 imza; ölü V1 PKI zinciri (`ToPKIRequestString` vb.,
  hiç çağrılmıyordu) atıldı. Süreç taşımaz — 4 slice ortak, feature'a gömülemez.
- **Kural**: handler metodu içinde Command/Query'den (kullanıcı) gelmeyen **HİÇBİR değer literal yazılmaz** —
  locale, endpoint yolları, kanal/grup/currency/itemType, `"success"` durumu, kart alias, email prefix+domain,
  id prefix'leri hepsi `Options/IyzicoRequestOptions` config POCO'sundan (appsettings, non-secret) okunur.
  Transport secret'ı (ApiKey/SecretKey/BaseUrl) ayrı `IyzicoProviderSettings` (user-secrets). Command/Query
  girdi sözleşmeleri (kullanıcıdan istenen param) **sabittir**.
- Domain-uygun 4 VO (`Buyer`/`Address`/`BasketItem` → Payments, `CardInformation` → StoredCards)
  `Domains/<Aggregate>/ValueObjects/`'ta; handler VO'dan slice-nested wire'a map'ler (anti-corruption sınır).
  `CardAssociationMapper` `Domains/StoredCards/`'da. Merchant/Commission iyzico wire kullanmaz.

## Merchant BC — onboarding + merchant CRUD

Merchant adayının başvurudan **Active** merchant'a yaşam döngüsü. Başvuru merchant DEĞİL, ayrı
**`RegisterRequest`** kaydıdır; merchant ancak onayla doğar (token verme **statü-kapılı ve kademeli**).

### Aggregate'ler

- **`RegisterRequest`** (Pending/Approved/Rejected) — başvuru alanları (domain, legalName, taxId,
  contactEmail…); uygunluk admin'in insan incelemesi. Mükerrer koruma domain-bazlı.
- **`Merchant`** — iyzico SubMerchant sözleşmesiyle **hizalı** alan seti (tip matrisi Personal/PrivateCompany/
  LimitedOrJointStockCompany, TR IBAN mod-97 + e-posta inline doğrulama), statü makinesi Active/Passive/
  Suspended, `"mk_"+Guid` MerchantKey (yalnız oluşturma yanıtında bir kez). `Domains/SubMerchants/` iyzico
  istemci hammaddesi **uyur** (SubMerchantKey hep null — ayrı iş).

### API

| Metod | Yol | Yetki |
|-------|-----|-------|
| `POST` | `api/v1/merchants` | `merchant.write` + `AdminPlaneOnly` |
| `GET` | `api/v1/merchants` | `AdminPlaneOnly` |
| `GET` | `api/v1/merchants/{merchantId}` | `MerchantScoped` (kendi kaydı) |
| `PUT` | `api/v1/merchants/{merchantId}` | `AdminPlaneOnly` |
| `PUT` | `api/v1/merchants/{merchantId}/status` | `AdminPlaneOnly` (merchant kendini askıdan çıkaramaz) |
| `POST` | `api/v1/register-requests/{id}/approve` · `/reject`, `GET /` | admin düzlemi |

Oluşturmada `MerchantCreated`, gerçek statü değişiminde `MerchantStatusChanged` **outbox**'la yayınlanır
(`merchant.lifecycle` fanout; aynı statü idempotent no-op). **Identity.Server** tüketir (OpenIddict istemci
senkronu). `RegisterRequests/Features/Agents/` slice'ları (submit/status ForAgent) var ama MCP yüzeyi
022'de söküldüğü için **dormant**.

## Commission BC — CommissionPolicy (024)

Merchant komisyonu = **iyzico işlem maliyeti + gateway marjı**. `CommissionPolicy` aggregate merchant başına
marj tanımını (yüzde + sabit) tutar; efektif komisyonu hesaplar. iyzico maliyeti işlem-sonrası rapordan
beslenecek (ileride). Eski 019 teklif/pazarlık (CommissionDraft/Proposal) ve banka komisyon grid'i SÖKÜLDÜ.

### API

| Metod | Yol | Slice |
|-------|-----|-------|
| `POST` | `api/v1/commission-policies` | CreateCommissionPolicy |
| `GET` | `api/v1/commission-policies` · `/{merchantId}` | List · Get |
| `PUT` | `api/v1/commission-policies/{merchantId}/margin` | UpdateCommissionPolicyMargin |
| `PUT` | `api/v1/commission-policies/{merchantId}/status` | ChangeCommissionPolicyStatus |
| `POST` | `api/v1/commission-policies/effective-commission` | CalculateEffectiveCommission |

Yetki: `commission.read` (sorgu) / `commission.write` (mutasyon).

## Kimlik ve yetki (011 + 012)

**Identity.Server** (`src/others/Identity.Server`) — OpenIddict tabanlı minimal **yalnız-makine IdP**.
Tek uç `connect/token`, yalnız `client_credentials`; sabit issuer **`https://localhost:5101`**. Kendi
`identityDb`'si (EF Core). Açılışta idempotent seed: scope'lar + istemciler (`admin-ui`, `payment-agent`);
secret'lar `Clients:<id>:Secret` config'ten. `scope` claim'i **JSON dizisi** (`ScopeClaimArrayHandler` —
tek-string yazımda policy'ler sessizce 403 verir).

**BC API koruması** — üç API JWT bearer (JWKS + audience) doğrular; her endpoint yetkisini açıkça beyan
eder (`RequireAuthorization`, sabitler `AuthorizationScopes`).

| Scope | Audience | Kullanan |
|-------|----------|----------|
| `merchant.read` / `.write` | `merchant.api` | admin-ui, merchant token |
| `commission.read` / `.write` | `commission.api` | admin-ui |
| `payment.read` / `.write` | `payment.api` | admin-ui, payment-agent |
| `payment.charge` / `cards.write` | `payment.api` | Active merchant token (çekim/vault yetenekleri) |

**Merchant istemci düzlemi (012 — G2):** merchant = OAuth istemcisi (`client_id = merchantId`,
`client_secret = MerchantKey`; MerchantKey yalnız `connect/token`'a gider, API'lere taşınmaz). Token'da
`merchant_id` claim'i; verme **statü-kapılı** (yalnız Active). Tenant enforcement `Common`'da:
`MerchantScopeEvaluator` + `MerchantScoped` (claim ↔ route `{merchantId}`, fail-closed) ve `AdminPlaneOnly`
(claim'li token giremez) policy'leri. İnsan login + RBAC sonraki dilimde (G3).

## Gateway (YARP)

`src/services/gateway/Gateway` — YARP reverse proxy; cluster adreslerini Aspire **service discovery**'den
çözer (`AddServiceDiscoveryDestinationResolver`). Route/cluster config-driven (`ReverseProxy` bölümü).
Auth şimdilik kabuk: `ClientCredential`/`Password` policy'leri yalnız geçerli token şart koşar
(grant-tipi ayrımı ertelenmiş auth işine ait).

## Altyapı (BC değil)

- **Mail.Worker** (016) — düz mail projesi, **MCP DEĞİL**. `mail.delivery` fanout'unu durable
  `mail.delivery-send` kuyruğuyla tüketir; `SendEmailHandler` `System.Net.Mail` → Mailpit. Retry
  `RetryWithCooldown(1s,5s,15s).Then.MoveToErrorQueue()`. Deterministik mailler BC handler'ından
  `[Transactional]` outbox `SendEmailRequested` ile (publish yalnız DB commit'te).
- **Excel.Mcp** — generic MCP (`document.generate`); MCP = yalnız agent/LLM yüzeyi (016 kuralı).
- **Payment.Agent / Merchant.Agent** — A2A host'ları (stateless, BC değil). 022'de MCP skill'leri söküldü;
  ödeme akışı/register/komisyon skill'leri **ölü** (proje derlenir), yeniden kurulmayı bekliyor.
- **Admin** — Razor Pages BFF (typed `HttpClient` + Aspire service discovery); çoğu ekran ölü, backend'e
  kural sızdırmaz. **Mailpit** — dev SMTP catch-all (SMTP :1025, web :8025).
- **MCP kuralı (016):** MCP tool'larını YALNIZ agent/LLM çağırır; servisler-arası / BC→altyapı iletişimi
  ASLA MCP değil (messaging veya HTTP).

## Test

Saf domain birim testleri (xUnit; DB/ağ/HTTP yok). Handler/HTTP/Razor/A2A/MCP entegrasyonu test edilmez
(quickstart ile elle doğrulanır). Toplam **107**:

- `tests/Payment.Api.Tests` (29) — StoredCard/Payment domain (VO doğrulama, statü, Model A).
- `tests/Merchant.Api.Tests` (47) — Merchant (IBAN mod-97, tip matrisi, statü makinesi), RegisterRequest,
  onboarding (`TryActivate`).
- `tests/Commission.Api.Tests` (31) — CommissionPolicy (marj yüzde+sabit, efektif komisyon, statü).

## Geliştirme akışı

Spec-driven (spec-kit): `specify → plan → tasks → implement`, değişikliklerde `converge`. Feature
artefaktları `specs/<NNN-feature>/`. Yorumlar, mesaj kodları ve commit'ler Türkçe. Karşı-uç (aday site)
işleri **ECommerceWithAgentFramework** repo'sundadır.

## Bilinçli ertelemeler

- **Canlı iyzico doğrulaması (037)**: transport sökümü sonrası sandbox charge/tokenize elle henüz denenmedi
  (JSON gövde + HMAC eskiyle bit-aynı olmalı — teyit bekliyor).
- **G3** — gateway portalına insan girişi (authorization_code + PKCE, rol + `merchant_id` claim'i); ASP.NET
  Identity deposu hazır (boş). Makine düzlemi (011) + merchant istemci (012) kararlı.
- iyzico payout/submerchant onboarding entegrasyonu (Merchant/Commission dormant wire) ileride; gerekince
  wire slice'a nested yeniden yazılır (Payment.Api deseni).
- Diğer BC'ler (Catalog, Order, Supplier…) tasarım gereği henüz yok; her biri kendi spec döngüsüyle eklenir.
