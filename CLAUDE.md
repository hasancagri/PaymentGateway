# CLAUDE.md

Claude Code'a bu repo'da rehberlik eder. **Gerçek-kaynak sırası:** kod + bu dosya >
Claude memory > Obsidian vault (`DropShop/`). Feature detayı BC haritasındaki `specs/*` yollarında.

**Mimari + kod konvansiyonları (taşınabilir katman): @docs/conventions.md** — DDD/VSA kuralları,
kod standartları, servisler-arası desenler orada (ECom'dan devralındı). Bu dosya yalnız BU projeye özeldir.

## Proje

**Banka-adaptörlü ödeme gateway'i** (eski adı DropShop; constitution v2.0.0 banka-adaptör pivotu). Ödeme yönü
**pay-by-link**: kart bilgisi sohbete/agent'a girmez, tek kullanımlık TTL'li link üzerinden PG'nin hosted
ödeme sayfasında toplanır, sonuç store'a webhook'la bildirilir. **Tek Payment BC** + destekleyen altyapı.

> **Durum (088 Phase-0 teardown TAMAM):** eski iyzico ödeme kanalı + Merchant BC + Commission BC +
> Mail.Worker + Admin BFF SÖKÜLDÜ. **Phase-1 (bank-adapter-skeleton) HENÜZ YAZILMADI** — hosted ödeme
> akışı kavramsal iskelet olarak durur, sağlayıcı çağrıları `PROVIDER_UNAVAILABLE` ile stub'lıdır;
> banka PaymentLink adapter'ı (anayasa İlke VII port/adapter) + yeni auth modeli Phase-1'de bağlanacak.

- **Payment** — kart-saklama yok (anayasa İlke VI). Hosted ödeme akışı: `HostedPaymentSession` aggregate +
  `POST /hosted-payment` (store başlatır) + `/internal/payments/callback/{callbackToken}` (secret-token
  kapılı sağlayıcı dönüşü, C1) + müşteri dönüş sayfası; terminal geçişte store'a HMAC-imzalı sonuç (durable
  outbox retry). **088: eski ödeme kanalı transport engine + MerchantKey/MerchantStatus X-Api-Key auth
  zinciri SÖKÜLDÜ.** PG bir MCP yüzeyi expose ETMEZ (anayasa İlke V).

## Komutlar

Repo kökünden. Çözüm: `PaymentGateway.slnx` (`dotnet build/test` otomatik bulur). Format/lint script'i YOK.

```bash
dotnet build                                              # tüm çözüm
dotnet run --project src/aspire/AppHost/AppHost.csproj    # tüm sistem (Aspire; Postgres + RabbitMQ)
dotnet test                                               # tüm testler (Payment.Api.Tests)
scripts/check-claude-spec-links.sh                        # BC haritası spec yolları guard'ı
scripts/check-flow-links.sh                                # FLOW.md domain-süreç anchor guard'ı (İLKE IX)
```

- **Sistemi hep Aspire AppHost'tan başlat**; servisler conn-string'i Aspire'dan alır, tek başına açılmaz.
- **Marten şeması otomatik kurulur** (`ApplyAllDatabaseChangesOnStartup`) — migration komutu yok.
- **Paket sürümleri yalnız `Directory.Packages.props`'ta** (CPM, istisnasız); `.csproj` sürümsüz listeler.

## BC haritası

Her BC = kendi DB'si + şeması. Origin sütunu = BC'yi tanımlayan spec'in tam yolu (guard'lı).
Servisler `src/services/*`; destek `src/others` (`Common`/`Shared`/`SharedKernel`/`Identity.Server`),
`src/aspire`. Aspire yalnız yaşayan servisleri ayağa kaldırır.

| Servis | DB | Ne yapar | Origin spec |
|---|---|---|---|
| `Payment.Api` | paymentDb | hosted ödeme akışı: `HostedPaymentSession` + `/hosted-payment` + secret-token'lı sağlayıcı callback + HMAC-imzalı store bildirimi + müşteri dönüş sayfası; kart-saklama yok; sağlayıcı entegrasyonu Phase-1 (bank adapter) | `specs/088-teardown-dead-services` |
| `identity-server` | identityDb | iç M2M OpenIddict (client_credentials); scope → audience; sabit issuer `https://localhost:5101`. Store→gateway ödeme yetkisi AYRI otoritede (AgentPlatform platform IdP) | `specs/011-openiddict-migration` |
| `gateway` | — | YARP reverse proxy; tek giriş (yalnız payment-route) | — |

- **Event akışı:** `PaymentCompleted`/`PaymentFailed` kontratları (Shared) hazır, tüketici Order BC gelince
  bağlanır. **088: merchant.lifecycle / merchant.commission / mail.delivery fanout'ları söküldü.**

## Projeye özel kurallar + tuzaklar

- **Banka wire = kullanan slice'ın İÇİNDE nested (anayasa İlke VII).** Paylaşılan banka SDK'sı ÜRETİLMEZ;
  her banka BC-sahipli adapter arkasında, wire request/response slice'ta nested kalır, domain sınırını
  GEÇMEZ; adapter wire'ı normalize domain cevabına çevirir (VO↔wire anti-corruption). *(Phase-1'de canlanır.)*
- **Handler'da literal YASAK.** Handler metodunda Command/Query'den (kullanıcı) gelmeyen HİÇBİR değer literal
  yazılmaz (locale/currency/endpoint/prefix...) → non-secret `Options/` POCO'sundan; transport secret'ı
  ayrı secret-store (user-secrets/env), asla non-secret config'le karışmaz (anayasa İlke VI).
- **Hosted ödeme auth (088 GEÇİCİ).** `/hosted-payment` şu an GEÇİCİ `payment.write` JWT scope ile korunur,
  tenant `MerchantId` gövdeden. **Nihai (Phase-1):** store→gateway = AgentPlatform platform IdP token (PG
  relying party, PG scope); müşteri/callback = anonim per-session secret token (fail-closed, anayasa İlke V).
- **Kontrol düzlemi + callback sır döndürmez (anayasa İlke VI).** `CallbackSecret` (store bildirim HMAC
  anahtarı) secret-store'da; ham gövde imzalanır, store aynı ham body'yi doğrular. Sır log/trace/dönüş DTO'sunda görünmez.
- **TUZAK (`ScopeClaimArrayHandler`):** scope claim JSON dizisidir; tek-string'te policy'ler sessizce 403
  verir — dokunma. Identity.Server sabit issuer `https://localhost:5101`.
- **Wolverine handler keşfi ad-son-eki bazlı:** `*Handler`/`*Consumer` ile bitmeyen handler taramayla
  BULUNMAZ → `opts.Discovery.IncludeType(...)` şart; "Handlers" (çoğul) sessizce keşfedilmez.

## Yapma listesi

- **Sökülenleri geri getirme (088 + öncesi):** iyzico ödeme kanalı + transport engine; Merchant BC +
  Commission BC + Mail.Worker + Admin BFF; `Iyzico.Provider` SDK; kart-vault/saved-card charge; CP.VPOS/
  BankRouter/Reference.Api; Payment.Agent/Merchant.Agent (A2A); PG `/mcp` yüzeyi. Gerekçe spec + memory'de.
- **Kart verisi tutma / PAN'ı PG'ye sokma** — anayasa İlke VI (PAN kalıcı olmaz, 3DS'te tarayıcıdan bankaya).
- **`IConfiguration`'dan doğrudan okuma** / handler'da literal (Options pattern).
- **PG'de MCP yüzeyi açma** (anayasa İlke V — ödeme akışı MCP'ye girmez; banka operasyon MCP'si ayrı repo).
- **Banka wire tipini paylaşılan lib'e çıkarma** — slice'ta nested kalır (anti-corruption, YAGNI).
