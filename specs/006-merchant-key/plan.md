# Implementation Plan: Merchant Key (gateway kimliği)

**Branch**: `006-merchant-key` | **Date**: 2026-08-02 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/006-merchant-key/spec.md`

## Summary

Merchant aggregate'ine, gateway'in onboarding'de mint ettiği benzersiz + değişmez + açık (gizli
olmayan) bir dış kimlik alanı — **MerchantKey** — eklenir. Key sunucu tarafında üretilir; çağıranın
gönderdiği değer yok sayılır. Create yanıtı ve tüm okuma sorguları key'i döndürür. Ayrıca key ile
merchant çözen bir sorgu (GetMerchantByKey) eklenir. Payment akışına bağlama, teslim/portal/bildirim
kapsam dışıdır (spec → Future Considerations).

**Teknik yaklaşım**: Mevcut Merchant slice desenini izler. Üretim + benzersizlik kontrolü handler'da
(Marten `IDocumentSession` orada), presence + değişmezlik aggregate'te. Key `Merchant.Create(...)`'e
parametre olarak girer; aggregate onu private-set immutable alanda saklar ve boş olamaz kuralını
uygular. `UpdateProfile` ve status metotları key'e dokunmaz (yapısal değişmezlik).

## Technical Context

**Language/Version**: C# / .NET 10 (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Marten (Postgres document store), Wolverine (in-proc bus + `[Transactional]`),
Minimal API + Asp.Versioning, Scrutor DI marker'ları. Yeni paket YOK.

**Storage**: Marten document store — `Merchant` dokümanı yeni `MerchantKey` string alanı kazanır.

**Testing**: Saf domain birim testleri (`tests/Merchant.Api.Tests`) — key presence + immutability.
Handler/HTTP entegrasyonu quickstart ile elle doğrulanır (proje konvansiyonu).

**Target Platform**: Linux/container; Aspire AppHost üzerinden ayağa kalkar (Postgres + RabbitMQ).

**Project Type**: Web service (tek bounded context — Merchant.Api). Payment.Api'ye DOKUNULMAZ.

**Performance Goals**: Ödeme-kritik yol değil; onboarding düşük hacim. Özel hedef yok.

**Constraints**: Key URL-güvenli, tek parça, boşluksuz. Gizli değil → hash/tek-seferlik yok.
Benzersizlik %100 korunur (çakışmada yeniden üret).

**Scale/Scope**: 1 aggregate değişikliği (Merchant), 1 command değişikliği (CreateMerchant),
2 query değişikliği (GetMerchant + GetAllMerchants response'a alan), 1 yeni query (GetMerchantByKey),
1 pure key generator, birim testleri. Migration yok (Marten şemasız doküman + seed/prod veri yok).

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası tekrar denetlenir.*

- **I. Bounded Context İzolasyonu**: PASS — yalnız Merchant.Api. Payment/Commission'a erişim yok,
  cross-BC çağrı yok. Key başka BC'ye sızmaz (Payment bağlama ertelendi).
- **II. Zengin Domain Modeli**: PASS — MerchantKey private-set, `Create` fabrikasında atanır,
  değiştiren metot yok (immutable invariant aggregate'te). Boş key reddi aggregate'te.
- **III. Vertical Slice + CQRS**: PASS — mevcut `CreateMerchant` (Command) genişler; `GetMerchantByKey`
  yeni Query slice'ı; repository yok, doğrudan `IDocumentSession`, endpoint `IMessageBus.InvokeAsync`.
- **IV. Result Pattern**: PASS — `FeatureObjectResultModel<T>` / `ResultDomain`, `MessageItem.Code`
  resource sabiti (`COMMON_MESSAGE_RECORD_NOT_FOUND` vb.), exception yok.
- **V. Merkezi Kimlik & Açık Yetki**: DEVIATION (kabul edilmiş) — endpoint'ler korumasız. Bu, tüm
  projede geçerli bilinçli AUTHZ ertelemesidir (TODO(AUTHZ_MODEL), Identity BC gelince). Bu feature
  yeni bir yetki ihlali getirmez; mevcut merchant endpoint'leriyle aynı durumdadır.
- **VI. Spec-Driven Development**: PASS — spec → plan → tasks akışı izleniyor.

Bloklayan ihlal yok. Complexity Tracking gerekmez.

## Project Structure

### Documentation (this feature)

```text
specs/006-merchant-key/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 — key formatı/üretim/benzersizlik kararları
├── data-model.md        # Phase 1 — Merchant.MerchantKey alanı + invariant'lar
├── quickstart.md        # Phase 1 — elle doğrulama senaryoları
├── contracts/
│   └── merchant-api.md   # Phase 1 — create/get/getByKey kontratları
└── checklists/
    └── requirements.md   # spec kalite checklist'i (mevcut)
```

### Source Code (repository root)

```text
src/services/Merchant.Api/Domains/Merchants/
├── Merchant.cs                                  # + MerchantKey alanı, Create imzası, presence kuralı
├── MerchantKeyGenerator.cs                      # YENİ — saf, URL-güvenli benzersiz token üretici
├── MerchantEndpointExtension.cs                 # + GetMerchantByKey endpoint kaydı
└── Features/
    ├── Commands/
    │   └── CreateMerchant.cs                     # handler: key üret + benzersizlik döngüsü + response'a ekle
    └── Queries/
        ├── GetMerchant.cs                        # response'a MerchantKey ekle
        ├── GetAllMerchants.cs                    # response'a MerchantKey ekle
        └── GetMerchantByKey.cs                   # YENİ — key ile merchant çöz

tests/Merchant.Api.Tests/
└── MerchantTests.cs                             # + key presence + immutability testleri
```

**Structure Decision**: Tek servis (Merchant.Api), mevcut vertical-slice düzeni. Yeni dizin/proje yok.
Payment.Api ve diğer BC'ler değişmez.

## Complexity Tracking

> Constitution Check'te bloklayan ihlal yok — bu bölüm boş.