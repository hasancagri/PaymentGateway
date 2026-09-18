# CLAUDE.md

Claude Code'a bu repo'da rehberlik eder. **Gerçek-kaynak sırası:** kod + bu dosya >
Claude memory > Obsidian vault (`DropShop/`). Feature detayı BC haritasındaki `specs/*` yollarında.

**Mimari + kod konvansiyonları (taşınabilir katman): @docs/conventions.md** — DDD/VSA kuralları,
kod standartları, servisler-arası desenler orada (ECom'dan devralındı). Bu dosya yalnız BU projeye özeldir.

## Proje

**iyzico ödeme gateway'i** (eski adı DropShop; 021-022 pivotuyla iyzico ödeme kanalına döndü).
Üç BC + destekleyen altyapı; her iş kendi spec döngüsüyle (`specs/<NNN>/`).

- **Payment** — kart-saklama söküldü (041 teardown). **Ödeme yönü hosted-CF CANLI (041):** HostedPaymentSession
  aggregate + `POST /hosted-payment` (X-Api-Key, Active-kapılı) iyzico CF initialize eder; iyzico dönüşü
  `/internal/payments/callback/{callbackToken}` (secret-token kapılı, CF retrieve teyidi) terminal'e taşır;
  store'a HMAC-imzalı sonuç (durable outbox retry) + müşteri dönüş sayfası. iyzico V2 wire (`Utils/*V2`) +
  MerchantStatus + ApiKey auth kalır. `/mcp` tool'suz (store HTTP çağırır, agent değil).
- **Merchant** — gateway müşterisi SİTE (pazaryeri/split DEĞİL); iyzico SubMerchant sözleşmesiyle hizalı
  alan seti + statü makinesi. OAuth istemci düzlemi (aşağıda). **Admin düzlemi MCP-only (043+044):**
  statü/liste/detay/güncelleme + başvuru onay/red tümü `merchant.admin` scope'lu MCP tool'ları;
  admin REST CRUD + register-requests grubu SÖKÜLDÜ (044). **Kişisel veri MCP'den geçmez (044):**
  Email/GSM/TCKN/IBAN/vergi no yalnız Admin hassas-veri sayfası + dar BFF çifti (`/merchants/{id}/sensitive`).
- **Commission** — komisyon politikası (iyzico maliyeti + marj). **MCP yüzeyi (043+044):** okuma
  `admin_get_commission_policy` + yazma `admin_create_commission_policy`/`admin_update_commission_margin`/
  `admin_change_commission_status` (commission.write, Wolverine scope middleware); admin REST +
  `CalculateEffectiveCommission` slice'ı SÖKÜLDÜ (hesap davranışı aggregate'te durur).
- Altyapı: **Identity.Server** (M2M OpenIddict), **Mail.Worker** (RabbitMQ→SMTP/Mailpit), **Admin**
  (Razor BFF — 044'te yalnız hassas-veri sayfası, CRUD ekranları YOK), **gateway** (YARP).
  Payment.Agent + Merchant.Agent (A2A) SÖKÜLDÜ; `/mcp` uçlarına dış MCP istemcisi (Claude desktop,
  `external-admin-agent`) doğrudan bağlanır. Ölü `payment-agent`/`merchant-agent` OAuth seed'leri
  silindi + açılışta store'dan prune (044).

## Komutlar

Repo kökünden. Çözüm: `PaymentGateway.slnx` (`dotnet build/test` otomatik bulur). Format/lint script'i YOK.

```bash
dotnet build                                              # tüm çözüm
dotnet run --project src/aspire/AppHost/AppHost.csproj    # tüm sistem (Aspire; Postgres + RabbitMQ + Mailpit)
dotnet test                                               # tüm testler (Payment/Merchant/Commission.Api.Tests)
scripts/check-claude-spec-links.sh                        # BC haritası spec yolları guard'ı
scripts/check-flow-links.sh                                # FLOW.md domain-süreç anchor guard'ı (İLKE VII)
```

- **Sistemi hep Aspire AppHost'tan başlat**; servisler conn-string'i Aspire'dan alır, tek başına açılmaz.
- **Marten şeması otomatik kurulur** (`ApplyAllDatabaseChangesOnStartup`) — migration komutu yok.
- **Paket sürümleri yalnız `Directory.Packages.props`'ta** (CPM, istisnasız); `.csproj` sürümsüz listeler.

## BC haritası

Her BC = kendi DB'si + şeması. Origin sütunu = BC'yi tanımlayan spec'in tam yolu (guard'lı).
Servisler `src/services/*`; destek `src/others` (`Common`/`Shared`/`SharedKernel`/`Identity.Server`/
`Mail.Worker`), `src/aspire`, `src/ui/Admin`. `src/agents/` boş (Payment.Agent+Merchant.Agent SÖKÜLDÜ).

| Servis | DB | Ne yapar | Origin spec |
|---|---|---|---|
| `Payment.Api` | paymentDb | kart-vault söküldü (041 teardown); **hosted-CF ödeme CANLI (041):** HostedPaymentSession + `/hosted-payment` (CF init) + secret-token'lı iyzico callback (CF retrieve) + HMAC-imzalı store bildirimi; iyzico V2 wire + MerchantStatus + ApiKey auth | `specs/041-hosted-cf-payment` |
| `Merchant.Api` | merchantDb | Merchant aggregate (SubMerchant hizalı); statü makinesi; `MerchantCreated`/`StatusChanged` outbox; **MCP-only admin düzlemi (044)**: tam yönetim MCP'de, REST'te yalnız MerchantScoped okuma + hassas-veri BFF çifti | `specs/044-mcp-only-admin-plane` |
| `Commission.Api` | commissionDb | CommissionPolicy (iyzico maliyeti + marj); **admin MCP okuma+yazma tool'ları (043+044)**; REST'te yalnız MerchantScoped okuma | `specs/044-mcp-only-admin-plane` |
| `identity-server` | identityDb | M2M OpenIddict (client_credentials); scope + merchant OAuth istemci düzlemi | `specs/011-openiddict-migration` |
| `Mail.Worker` | — | RabbitMQ `mail.delivery` → SMTP/Mailpit; retry→error queue; ClosedXML ek | — |
| `Admin` | — | Razor BFF — 044'te tek iş yüzeyi hassas-veri sayfası (`/Merchants/Sensitive`); CRUD ekranları söküldü | — |
| `gateway` | — | YARP reverse proxy; tek giriş | — |

- **Event akışı:** Merchant `merchant.lifecycle` fanout (`MerchantCreated`/`MerchantStatusChanged`, statü
  string taşır, enum sızmaz) → Identity.Server tüketir (OpenIddict istemci senkronu). `PaymentCompleted`/
  `PaymentFailed` kontratları hazır, tüketici Order BC gelince bağlanır.
- **Payment MerchantStatus:** event-fed statü referansı (aggregate DEĞİL); çekim öncesi Active kontrolü
  (fail-closed — makine token'ı statü taşımaz, kapı gateway içinde).

## Projeye özel kurallar + tuzaklar

- **iyzico wire = slice'ın İÇİNDE nested.** Paylaşılan SDK YOK (`Iyzico.Provider` söküldü). Her iyzico
  wire request/response, kullanan slice'ta nested düz camelCase JSON POCO; yanıtlar
  `Payment.Api.Utils.ProviderResourceV2`'den türer. Wire tipleri BC DIŞINA SIZMAZ; VO↔wire map anti-corruption.
- **iyzico transport engine** `Payment.Api/Utils`'te tek kopya (5 dosya: RestHttpClientV2/ProviderResourceV2/
  HashGeneratorV2/ProviderConstants/ProviderOptions); süreç taşımaz, 4 slice ortak — feature'a gömülemez.
- **Handler'da literal YASAK.** Handler metodunda Command/Query'den (kullanıcı) gelmeyen HİÇBİR değer literal
  yazılmaz (locale/currency/itemType/endpoint/alias/prefix...) → `Options/IyzicoRequestOptions` POCO'sundan
  (non-secret; transport secret'ı ayrı `IyzicoProviderSettings`).
- **Merchant OAuth istemci düzlemi.** Merchant = OAuth istemcisi (`client_id=merchantId`,
  `client_secret=MerchantKey`; MerchantKey yalnız `connect/token`'a gider). Token statü-kapılı (yalnız Active).
  `MerchantScoped` (claim-route eşleşmesi, fail-closed) + `AdminPlaneOnly` (claim'li token giremez).
- **TUZAK (`ScopeClaimArrayHandler`):** scope claim JSON dizisidir; tek-string'te policy'ler sessizce 403
  verir — dokunma. Identity.Server sabit issuer `https://localhost:5101` (ECom Identity 5001; A2A'da ikisi birlikte koşar).

## Yapma listesi

- **Sökülenleri geri getirme:** `Iyzico.Provider` paylaşılan SDK + V1 PKI zinciri; `Excel.Mcp` +
  `document.generate` scope; CP.VPOS/BankRouter/Reference.Api (022 pivotu); `Payment.Agent`/
  `Merchant.Agent` (A2A host'ları, 038/043) + Admin `AgentChat`/`RegisterRequests` sayfaları.
  Gerekçe ilgili spec + memory'de.
- **iyzico wire tipini paylaşılan lib'e çıkarma** — slice'ta nested kalır (ikinci canlı tüketici çıkana dek YAGNI).
- **`IConfiguration`'dan doğrudan okuma** / handler'da literal (Options pattern + IyzicoRequestOptions).
- **MCP'yi agent-dışı koddan** imperatif çağırma; servisler-arası MCP değil (messaging/HTTP).
- **Wolverine event-handler'ı "Handlers" (çoğul) adlandırma** — sessizce keşfedilmez (bkz. conventions TUZAK).
