# Implementation Plan: Card Vault / Tokenization (Kart Saklama)

**Branch**: `017-card-vault-tokenization` | **Date**: 2026-08-10 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/017-card-vault-tokenization/spec.md`

## Summary

Payment BC'ye kartın kayıt-otoritesi olan bir **StoredCard** aggregate'i + kalıcılığı eklenir.
Merchant (ECommerce üzerinden) PAN'ı gönderir, Gateway Luhn/expiry doğrular, `bin`/`last4`/`brand`
türetir, PAN'ı enc-at-rest (dev simüle) saklar ve **yalnız opak token** döner. Ödeme akışı token'ı
mevcut `ICardVault.ResolveCardInfoAsync` sözleşmesiyle çözer (arkadaki kaynak simüle fixture yerine
gerçek StoredCard). Vault yazımları (tokenize/update/revoke) merchant-scoped Command slice'larıdır;
silme soft (Revoked). PAN Payment BC sınırını hiçbir biçimde ham geçmez.

## Technical Context

**Language/Version**: C# / .NET 10 (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Marten (Postgres document store), Wolverine (in-proc bus + `[Transactional]`
outbox), ASP.NET Minimal API + Asp.Versioning, OpenIddict (JwtBearer scope/policy), Scrutor (DI marker)

**Storage**: Marten/Postgres — yeni document type `StoredCard` (`opts.Schema.For<StoredCard>()`),
identity = `Token` (string; BinCard'ın `.Identity(x => x.BinNumber)` deseni), index `MerchantId`

**Testing**: Saf domain birim testleri — `tests/Payment.Api.Tests` (yoksa kurulur): StoredCard
invariant'ları (Luhn, expiry, immutable PAN/token, revoke idempotent, update kuralları)

**Target Platform**: Linux/container, Aspire orchestration (Postgres + RabbitMQ)

**Project Type**: Mikroservis BC (Payment.Api) — web service, vertical slice + CQRS

**Performance Goals**: Etkileşimli ödeme yolu; tokenize/resolve tekil kayıt işlemleri, özel hedef yok

**Constraints**: Ham PAN Payment BC dışına çıkmaz (yanıt/log/event); PAN slice sınırını ham geçmez;
yalnız TL (kartta para birimi yok); vault Active-only (ödeme düzlemi kapısı)

**Scale/Scope**: Yeni 1 aggregate + 3 command slice + 1 resolve implementasyon değişimi + 1 auth
düzlemi genişlemesi (payment plane → Active merchant). ~orta boy feature, tam akış.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli, Phase 1 sonrası yeniden kontrol.*

- **I. BC İzolasyonu** ✓ — PAN yalnız Payment BC'de. ECommerce'e giden tek şey opak token (kontrat).
  Paylaşılan DB/model yok. StoredCard yalnız Payment BC document'i.
- **II. Zengin Domain** ✓ — `StoredCard` private setter + statik `Create` fabrikası + davranışlar
  (`UpdateDetails`, `Revoke`); invariant'lar (Luhn, expiry, PAN/token immutable) aggregate'te yakalanır.
- **III. Vertical Slice + CQRS** ✓ — `Domains/StoredCards/Features/Commands/{TokenizeCard,UpdateCard,
  RevokeCard}`. **Repository YOK**: handler'lar `IDocumentSession` kullanır. `ICardVault` yazım metodu
  ALMAZ (repository olur) — resolve-only kalır; ödeme akışının query-side abstraksiyonu.
- **IV. Result Pattern** ✓ — aggregate `ResultDomain`, handler `FeatureObjectResultModel<T>`,
  `MessageItem` + resource `Code`. Exception yok.
- **V. Merkezi Kimlik ve Açık Yetki** ✓ (analyze C1 sonrası DÜZELTİLDİ) — Vault uçları yeni
  **capability scope `cards.write`** + `MerchantScoped`, route `merchants/{merchantId:guid}/vault/cards`.
  Bu, Payment audience'ını Active merchant token'ına **yalnız vault için** açar (bugün kapalı, 012).
  Anayasa V "Active tam demet" bunu öngörür → mevcut ilkenin uygulanışı, amendment gerekmez.
  **`payment.write` merchant'a VERİLMEZ** — Payment `/mcp` (agent yüzeyi) + `/pos-accounts` merchant'a
  kapalı kalır (C1 deliği kapatıldı). Capability scope anayasa yasağına girmez (yasak yalnız
  merchant/statü-başına çoğaltmadır; `cards.write` yetki TÜRÜdür, `mail.send`/`document.generate`
  precedent'i). Charge fail-closed korunur (Provisioning `cards.write` almaz, FR-017). Tenant
  izolasyonu token↔merchant eşleşmesiyle (MerchantScoped fail-closed).
- **VI. Spec-Driven** ✓ — bu akış.

**Tech kısıtları**: Marten explicit `Schema.For<StoredCard>()` (Program.cs), Wolverine handler keşfi,
Aspire conn-string, CPM. CP.VPOS sınırı: PAN'ı BU feature CP.VPOS'a vermez (charge dışı); Luhn saf.

**Karar (V. ilke genişlemesi) kullanıcı onayı bekler** — completion report'ta işaretlendi.

## Project Structure

### Documentation (this feature)

```text
specs/017-card-vault-tokenization/
├── plan.md              # bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   └── vault-api.md     # Phase 1 — HTTP kontratı
└── tasks.md             # /speckit-tasks (bu komutta ÜRETİLMEZ)
```

### Source Code (repository root)

```text
src/services/Payment.Api/
├── Domains/
│   └── StoredCards/                      # YENİ aggregate klasörü (tek AggregateRoot)
│       ├── StoredCard.cs                 # aggregate: Create / UpdateDetails / Revoke
│       ├── StoredCardStatus.cs           # enum: Active, Revoked
│       ├── CardBrand.cs                  # enum/Enumeration: Visa, Mastercard, Amex, Troy, Unknown
│       ├── StoredCardEndpointExtension.cs
│       └── Features/
│           └── Commands/
│               ├── TokenizeCard.cs       # POST  .../vault/cards
│               ├── UpdateCard.cs         # PUT   .../vault/cards/{token}
│               └── RevokeCard.cs         # DELETE .../vault/cards/{token}
├── CardVault/
│   ├── ICardVault.cs                     # DEĞİŞMEZ (resolve-only sözleşme)
│   ├── SimulatedCardVault.cs             # DEĞİŞİR → StoredCard'tan çözer (fixture kalkar)
│   ├── IPanProtector.cs                  # YENİ — enc-at-rest soyutlaması (dev simüle)
│   └── DevPanProtector.cs               # YENİ — reversible dev impl (ISingletonDependency)
├── PanTools/                             # veya SharedKernel: LuhnValidator, BinExtractor, BrandDetector (saf)
└── Program.cs                            # Schema.For<StoredCard>() + AddStoredCardGroupEndpointExtension

src/others/Common/ (AuthorizationScopes) # YENİ sabit: CardsWrite = "cards.write"
src/others/Identity.Server/              # cards.write scope kaydı + Active merchant demetine ekle (statü-kapılı)
src/services/Payment.Api/ (auth wiring)  # merchant token cards.write kabulü + MerchantScoped policy

tests/Payment.Api.Tests/                 # YENİ (yoksa) — StoredCard domain birim testleri
```

**Structure Decision**: Mevcut Payment.Api vertical-slice düzenine oturur. Aggregate klasör kuralı:
`Domains/StoredCards/` tek `: AggregateRoot` (StoredCard); enum/status/endpoint-extension kök muaf.
Vault altyapısı (`ICardVault`/protector/pan-tools) `Domains/` dışında (altyapı, domain feature değil),
Payment.Api mevcut `CardVault/` konumunu sürdürür. Yazım feature'ları `Features/Commands/` altında.
Bu, Payment.Api'nin **ilk merchant-scoped** uç grubudur (route'ta `{merchantId:guid}`).

## Complexity Tracking

> Anayasa ihlali yok — V. ilke genişlemesi mevcut "Active tam demet" ilkesinin uygulanışıdır,
> yeni karmaşıklık/istisna değil. Doldurulacak satır yok.