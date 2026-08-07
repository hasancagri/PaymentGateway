# PaymentGateway (DropShop)

Tedarikçi ürünlerini dropship modeliyle satan e-ticaret sisteminin ödeme altyapısı. Her
mikroservis bir **Bounded Context**; Vertical Slice + CQRS, zengin aggregate'ler, Result pattern.
Altyapı: **Aspire** (orkestrasyon), **Marten** (Postgres document store), **Wolverine** (in-process bus).

## Komutlar

```bash
dotnet build                                             # tüm çözüm (PaymentGateway.slnx)
dotnet run --project src/aspire/AppHost/AppHost.csproj   # sistemi Aspire ile başlat (Postgres + RabbitMQ)
dotnet test tests/Payment.Api.Tests                      # saf domain birim testleri (Payment)
dotnet test tests/Merchant.Api.Tests                     # saf domain birim testleri (Merchant)
dotnet test tests/Commission.Api.Tests                   # saf domain birim testleri (Commission)
```

Sistemi her zaman AppHost üzerinden başlatın; servisler bağlantı dizelerini Aspire'dan alır.
Central Package Management açık (sürümler `Directory.Packages.props`); tek istisna `CP.VPOS`.

## Yapı

```text
src/
├── aspire/           AppHost + ServiceDefaults (orkestrasyon)
├── agents/
│   └── Payment.Agent A2A host + LLM router + MCP client (007) — BC değil, stateless adaptör
├── services/
│   ├── Payment.Api   Ödeme BC (CP.VPOS sanal POS + BinCard katalog + A2A ödeme oturumu/MCP)
│   ├── Merchant.Api  Merchant BC (onboarding + settlement hesapları)
│   ├── Commission.Api Komisyon BC (banka + komisyon)
│   └── Reference.Api Referans veri BC (event-only, HTTP yüzeyi yok — 010)
├── ui/Admin          Razor Pages yönetim arayüzü (makine token'ıyla API çağırır)
└── others/           Common (domain base, Result, auth) + Shared (integration event)
                      + Identity.Server (OpenIddict M2M IdP — 011)
```

Bir feature = bir static class (record command/query + Response + Handler + endpoint). Handler'lar
`[Transactional]` + `IDocumentSession`; sonuçlar `FeatureObjectResultModel<T>`/`ResultDomain`
(exception değil). CP.VPOS tipleri slice sınırını geçmez.

## Bounded Context'ler

| BC | Sorumluluk |
|----|-----------|
| **Payment** | Sanal POS ödeme; `BankRouter` maliyet-sıralı banka adayları; `PosAccount` aggregate (banka anlaşması + komisyon); BIN kart katalogu (008); A2A ödeme oturumu + MCP tool'ları (007). |
| **Merchant** | Merchant onboarding + API key; settlement (payout) banka hesapları. |
| **Commission** | Banka referansı, banka komisyonları, merchant komisyonları. |

## Merchant BC — Settlement hesapları (feature 004) + Admin ekranları (005)

Merchant'a payout için para yatırılacak banka hesabı yönetimi. `MerchantSettlementAccount` aggregate;
`Merchant` aggregate'ine dokunulmaz, bağ `MerchantId` referansıyla.

### MerchantSettlementAccount aggregate

- **BankCode** (4 hane), **Iban** (normalize saklanır), **AccountOwnerName**, **AccountNo**/
  **AccountDescription** (opsiyonel), **Status** (`Active`/`Passive`, soft — silme yok).
- IBAN doğrulama saf aggregate içinde: `^TR\d{24}$` + **ISO 13616 mod-97**. Yalnız TR (yurtiçi TL).
- Banka referansı yerel `BankCatalog` kopyasına (Commission ile elle senkron) doğrulanır — cross-BC
  çağrı yok. Merchant varlığı + mükerrer IBAN handler'da (Marten sorgu).

### API

| Metod | Yol | Açıklama |
|-------|-----|----------|
| `POST` | `/merchants/{merchantId}/settlement-accounts` | Ekle. |
| `GET` | `…/settlement-accounts` | Merchant'ın hesapları (tenant-scoped). |
| `GET` | `…/settlement-accounts/{accountId}` | Detay (başka merchant → 404). |
| `PUT` | `…/settlement-accounts/{accountId}` | Güncelle. |
| `PUT` | `…/settlement-accounts/{accountId}/status` | `{ isActive }` aktif/pasif. |

Doğrulama kodları: `INVALID_FORMAT` (IBAN/bankCode), `RECORD_NOT_FOUND` (merchant/banka), `RECORD_DUPLICATE`.

### Admin arayüzü (005)

Gateway admin için (merchant self-service değil). Merchant detay → **Settlement Hesapları**: liste
(banka kod+ad, IBAN, sahip, durum), ekleme (banka dropdown Commission katalogundan), düzenleme + aktif/
pasif. Salt-UI — backend'e dokunmaz; API sonucunu `MessageText` ile Türkçe gösterir.

## Commission BC — Banka referansı + komisyon grid (feature 002)

Komisyon BC'ye banka yönetimi ve boşluksuz komisyon girişi eklendi.

### Bank aggregate

- **Code** (4 hane, immutable, iş anahtarı), **Name** (kanonik katalogdan türer, immutable),
  **IsActive**, **SupportedInstallments** (`List<int>`, 1..15, distinct + artan). Sabit `MaxInstallment = 15`.
- `Create(code, installments)` / `Update(isActive, installments)` / `SoftDelete()`.
- **Seed yok** — DB boş başlar; operatör bankaları katalogdan seçerek ekler.

### Kanonik banka katalogu

Seçilebilir bankaların sabit listesi (`BankCatalog`) — CP.VPOS `BankService.AllBanks`'ten kopyalanan
48 banka (Code + Name). CP.VPOS'a çalışma-zamanı bağımlılığı yoktur (`AllBanks` `internal`; değerler
statik gömülü). Operatör banka adı/kodunu **elle yazmaz**, katalogdan seçer; ad ve kod immutable.

### API

| Metod | Yol | Açıklama |
|-------|-----|----------|
| `GET` | `/banks/catalog?onlyAvailable` | Seçilebilir katalog (eklenmişleri eler). |
| `POST` | `/banks` | `{ code, supportedInstallments }` — ad katalogdan türer. |
| `GET` | `/banks?includeInactive` | Liste. |
| `GET` | `/banks/{code}` | Detay. |
| `PUT` | `/banks/{code}` | `{ isActive, supportedInstallments }` — kod/ad değişmez. |
| `DELETE` | `/banks/{code}` | Soft-delete (bağlı komisyon varsa reddedilir). |
| `POST` | `/bank-commissions/bulk` | Atomik toplu upsert (grid kaydı). |
| `GET` | `/bank-commissions/criteria-options` | Kriter enum'ları (tek kaynak). |

Doğrulama kodları: `BANK_NOT_IN_CATALOG` (katalog-dışı kod), `BANK_HAS_COMMISSIONS` (bağlı komisyonlu
banka silinemez), `RECORD_DUPLICATE`, `INVALID_RANGE`.

### Admin arayüzü

- **Bankalar** — katalog selectbox ile ekle, taksit 1..15 checkbox grid; Edit'te kod+ad salt-görünüm.
- **Komisyon grid** — banka seç → `CardBrand × CardType × TransactionRegion × taksit` tam kombinasyon;
  eksik hücreler işaretli; **eksen filtresi** + **20'li sayfalama** + **görünen-boş toplu doldur**;
  tek işlemde kaydet.
- **Komisyon listesi** — banka adı gösterimi + eksen filtresi + 20'li sayfalama.
- Filtre/sayfalama/doldur davranışı jenerik `wwwroot/js/filterable-table.js` modülünde (grid + liste ortak).

## Payment BC — A2A Ödeme Oturumu (feature 007)

Kayıtlı kart **token**'ı ile A2A üzerinden **taksit seçimine kadar** akış. Kart verisi (PAN/CVV)
LLM/A2A/MCP kanalından **geçmez** — yalnız token + tutar + seçilen taksit. Fiyatlama **Model A**:
kullanıcı sepet tutarını öder; banka komisyonu yalnız en ucuz POS'u seçmek için `BankRouter`'a girer.
**Fiili çekim (pay) 007 dışı** — seçilen taksit sonraki pay feature'ına seam ile devredilir.

**BIN-bazlı read-only quote (feature 024).** E-ticaret tarafının **quote-only** akışı için token/oturum
GEREKTİRMEYEN ikinci bir taksit sorgusu eklendi: girdi = kartın **BIN**'i (ilk 6 hane, hassas değil) +
sepet tutarı. BIN → `CardInfo` (`ResolveBinCard`) çözülür, taksit listesi token akışıyla **aynı pure**
`BuildOfferedInstallments` (Model A) ile üretilir; **oturum açılmaz, hiçbir şey yazılmaz**. Token/PAN/CVV
kabul etmez. Token+oturum akışı (`get_installment_options` → `select_installment`) charge için korunur;
bu ikisi bilinçli olarak ayrı tutulur (quote yalnız bankayı = BIN'i ister, charge kart kimliğini/token'ı).

### Bileşenler

- **`src/agents/Payment.Agent`** — A2A host (`AddA2AServer` + `MapA2AJsonRpc` + `MapWellKnownAgentCard`)
  + LLM router (`ChatClientAgent`, Microsoft Agent Framework) + MCP client. BC değil, stateless. LLM
  yalnız tool sırasını kurar (quote → select); tutar/banka/kart üretmez (domain'den).
- **`Payment.Api/Domains/PaymentSessions`** — `PaymentSession` aggregate (faz makinesi:
  `Opened → QuoteProvided → InstallmentSelected / Failed`). Agent'a açık slice'lar `Features/Agent/`;
  MCP tool'ları `PaymentSessionMcpTools.cs` (ince `[McpServerToolType]` sarmalayıcı, `IMessageBus` ile
  slice sarar). MCP server: `AddMcpServer().WithToolsFromAssembly()` + `MapMcp("/mcp")`.
- **`Payment.Api/CardVault`** — `ICardVault` seam; `SimulatedCardVault` token→BIN simüle eder, BIN→kart
  çözümü 008 `ResolveBinCard`'tan (gerçek tokenizasyon ayrı feature).

### MCP tool'ları (agent yüzeyi)

| Tool | İş |
|------|-----|
| `quote_installments_by_bin` | **BIN** (ilk 6 hane) + sepet tutarı → Model A taksit listesi. **Read-only**, oturum açmaz, token/PAN/CVV kabul etmez (024). |
| `get_installment_options` | token + sepet tutarı → Model A taksit listesi + `sessionId` (oturum açılır). |
| `select_installment` | `sessionId` + taksit → seçimi oturuma yazar (`InstallmentSelected`). Çekim yapmaz. |
| `payment_status` | `sessionId` → güncel faz. |

Bir MCP client (ör. Claude Desktop, `mcp-remote` ile `http://<payment-api>/mcp`) doğrudan bağlanıp
bu tool'ları çağırabilir; o zaman router LLM = client'ın kendisidir (Payment.Agent bypass). E-ticaret
agent'ı ise Payment.Agent'a **A2A** ile bağlanır. Agent Card skill'leri: **`installment_quote`**
(BIN-bazlı read-only quote, e-ticaret 024 tüketir) + `quote-installments` (token+oturum, charge yolu).

> Preview paketler (A2A / Agent Framework) `Directory.Packages.props`'ta pin'lidir. Payment.Agent
> chat anahtarını kendi config'inden alır (`OpenAI:ApiKey`, user-secrets).

## Kimlik ve yetki (feature 011)

**Identity.Server** (`src/others/Identity.Server`) — OpenIddict 7.6 tabanlı minimal **yalnız-makine
IdP**. Tek uç `connect/token`, yalnız `client_credentials`; sabit issuer **`https://localhost:5101`**
(dev cert). Kendi `identityDb`'si (EF Core + tek Initial migration). Açılışta idempotent seed:
6 scope + 2 istemci (`admin-ui`, `payment-agent`); secret'lar `Clients:<id>:Secret` config
anahtarından (koda gömülü değil). Access token düz imzalı JWT; **`scope` claim'i JSON dizisidir**
(`ScopeClaimArrayHandler` — tek-string yazımda servislerin scope policy'leri sessizce 403 verir).

**BC API koruması** — üç API JWT bearer (JWKS keşfi + audience) doğrular; her endpoint yetkisini
açıkça beyan eder: `GET → <bc>.read`, mutasyon → `<bc>.write` (`RequireAuthorization`, sabitler
`AuthorizationScopes`). Payment'ın `/mcp` yüzeyi tek policy ile korunur: `payment.write`.
Reference.Api'nin HTTP yüzeyi yok — kapsam dışı. Sağlık/doc uçları anonim.

| Scope | Audience | Kullanan |
|-------|----------|----------|
| `merchant.read/.write` | `merchant.api` | admin-ui |
| `commission.read/.write` | `commission.api` | admin-ui |
| `payment.read/.write` | `payment.api` | admin-ui, payment-agent |

**İstemciler** — Admin BFF (`AdminTokenHandler`) ve Payment.Agent (`AgentTokenHandler`):
client_credentials token'ı static cache'lenir, süresine 30 sn kala (veya dolmuşsa anında)
yenilenir; token edinilemezse hata yüzeye çıkar (sessiz başarı yok).

## Merchant istemci düzlemi (feature 012 — G2)

Merchant = OAuth istemcisi: **`client_id = merchantId`, `client_secret = MerchantKey`** (006'nın
`mk_+Guid` değeri; üçüncü sır yok). MerchantKey yalnız `connect/token`'a gider — BC API'lerine
asla taşınmaz (API-key deseni bilinçli reddedildi). Access token ömrü **global 15 dk**
(revocation kolu; Admin/Agent handler'ları proaktif yenilediği için davranış değişmez).

- **Otomatik istemcileşme (event-driven):** Merchant.Api onboarding'de `MerchantCreated`,
  statü değişiminde `MerchantStatusChanged` yayınlar (`merchant.lifecycle` fanout);
  Identity.Server `MerchantClientEventHandlers` ile tüketip OpenIddict kaydını idempotent
  upsert eder. Backfill yok (dev fazı — ortam sıfırlanır).
- **Status-gated issuance:** yalnız `Active` merchant token alır. Suspended/Passive'de istemci
  SİLİNMEZ; izinleri boşaltılır (`unauthorized_client`) — reaktivasyonda secret yeniden taşınmaz.
- **`merchant_id` claim'i:** application `Properties`'ten `TokenEndpoint`'e; yalnız merchant
  istemcilerinde bulunur (admin-ui/payment-agent claim'siz = global davranış).
- **Tenant enforcement (`Common`):** `MerchantScopeEvaluator` (saf çekirdek, birim testli) +
  iki policy — `MerchantScoped` (claim ↔ route `{merchantId}` eşleşmesi; route'ta değer yoksa
  fail-closed RET → liste/by-key/create merchant token'ına kapalı; uyuşmazlık 403) ve
  `AdminPlaneOnly` (claim'li token giremez — `PUT merchants/{merchantId}/status`, merchant
  kendini askıdan çıkaramaz). Uçlar policy'yi `RequireAuthorization` ile açıkça beyan eder.
- **Erişim alanı:** merchant token'ı yalnız Merchant BC'de kendi kaydı + settlement-account
  uçları; Payment/Commission audience uyuşmazlığıyla 401.

İnsan login + merchant'a bağlı kullanıcı/rol (RBAC) sonraki dilimde (G3); storage düzeyi tenant
izolasyonu (Marten conjoined tenancy) 013'te.

## Test

Saf domain birim testleri; handler/HTTP/Razor Pages/A2A/MCP/LLM entegrasyonu test edilmez (quickstart ile elle).

- `tests/Payment.Api.Tests` — `PaymentSession` faz geçişleri + Model A taksit hesabı (008 BinCard testleri dahil).
- `tests/Merchant.Api.Tests` — `MerchantTests`, `MerchantSettlementAccountTests` (IBAN mod-97, TR kısıtı,
  durum geçişleri).
- `tests/Commission.Api.Tests` — `BankTests` (aggregate + katalog), `BulkUpsertCriteriaMatchTests`,
  `BankCommissionTests`, `MerchantCommissionTests`.

## Geliştirme akışı

Spec-driven (spec-kit): `specify → plan → tasks → implement`, değişikliklerde `converge`. Feature
artefaktları `specs/<NNN-feature>/`. Yorumlar, mesaj kodları ve commit'ler Türkçe.

## Bilinçli ertelemeler

- Makine düzlemi yetki (011) kararlı. Ertelenen iki kimlik düzlemi: **G2** — merchant'ın
  sunucusu işlem için istemcileşir (`client_id=merchantId`, `client_secret=MerchantKey`;
  MerchantKey yalnız token exchange'de, istek başına taşınmaz); **G3** — gateway portalına
  insan girişi (authorization_code + PKCE, `sub=userId`, rol + `merchant_id` claim'i;
  MerchantKey insan akışında hiç kullanılmaz). ASP.NET Identity deposu G3 için hazır (boş).
- Diğer BC'ler (Catalog, Order, Supplier…) tasarım gereği henüz yok; her biri kendi spec döngüsüyle eklenir.