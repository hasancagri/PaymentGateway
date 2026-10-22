# Implementation Plan: PaymentLink Üretimi (Pay-by-Link ilk dilim)

**Branch**: `089-payment-link-creation` | **Date**: 2026-10-20 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/089-payment-link-creation/spec.md`

## Summary

Phase-1 ilk dikey dilim: 088'de korunan `HostedPaymentSession` tohumu `PaymentLink` aggregate'ine evrilir. Store S2S ile tek-kullanımlık TTL'li ödeme linki üretir (CreatePaymentLink); müşteri linki anonim gizli açılış token'ıyla açar, **kart formundan önce** güven bağlamını görür (store adı + sipariş özeti + tutar + son geçerlilik) ve link Opened'a geçer; "Öde" sağlayıcıya gitmez — `PROVIDER_UNAVAILABLE` stub döner. Yaşam döngüsü: **Pending → Opened → terminal {Paid(erişilmez), Expired, Cancelled}**. Expiry lazy (erişim anında) hesaplanır; Opened oturuma TTL dolumunda ~5 dk grace. Tekillik aktif-duruma filtreli (partial unique): terminal sonrası aynı (store, orderRef) ile yeni link serbest. Banka/simülatör + çekim + 3DS KAPSAM DIŞI (İlke VII sonraki dilim).

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable`, `ImplicitUsings` açık)

**Primary Dependencies**: Marten (Postgres doküman + index), Wolverine (süreç-içi command bus + `[Transactional]` outbox), Minimal API (endpoint), OpenIddict JWT bearer (`payment.write` — geçici), .NET Aspire (orkestrasyon). Yeni paket YOK (CPM; `Directory.Packages.props` değişmez).

**Storage**: Postgres / Marten, `paymentDb` + Payment şeması. `PaymentLink` **düz doküman** (event stream DEĞİL — link ömrü/oturum belgesidir, seed `HostedPaymentSession` deseninin devamı). Marten `ApplyAllDatabaseChangesOnStartup` ile şema otomatik.

**Testing**: xUnit saf domain birim testleri (`tests/Payment.Api.Tests`, mevcut `HostedPaymentSessionTests` evrilir). Host/entegrasyon harness'ı yok; Domain-TDD (İlke VIII) — aggregate testleri impl'den önce.

**Target Platform**: Linux server (Aspire AppHost üzerinden; tek servis izole çalışmaz).

**Project Type**: Tek BC web-service (`src/services/Payment.Api`).

**Performance Goals**: SC-001 — geçerli istekte **< 1 sn** link üretimi (tek doküman yazımı + token üretimi; dış çağrı yok).

**Constraints**: Yalnız TRY (alan kısıtı/İlke VI). PAN hiçbir aşamada PG'ye girmez (bu dilimde zaten sağlayıcıya iletilmez). Açılış yüzeyi anonim + fail-closed (gizli token, İlke V). Expiry lazy — proaktif watchdog YOK.

**Scale/Scope**: Store-facing tek BC; 1 aggregate (PaymentLink), 4 endpoint, 3-4 command slice, ~1 query (güven bağlamı render'ı). Mütevazı hacim.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden denetlenir.*

| İlke | Durum | Not |
|---|---|---|
| I. BC İzolasyonu | ✅ | Tek Payment BC, kendi DB/şema. Store adı + sipariş özeti **store tarafından istekte sağlanır** (ölü Merchant BC'ye erişim YOK). Banka wire tipi yok (stub). |
| II. Zengin Domain | ✅ | `PaymentLink : AggregateRoot`, private setter + statik `Create` + davranış (`Open`/`Cancel`/`Expire`). Invariant (TTL/terminal/tek-kullanım) aggregate'te. Enum `PaymentLinkStatus` aggregate dosyasında. |
| III. VSA + CQRS | ✅ | `Features/Commands/{CreatePaymentLink, OpenPaymentLink, CancelPaymentLink, PayPaymentLink}`. Handler doğrudan `IDocumentSession`; repository yok. **Bilinçli sapma:** GET açılış ucu durum geçişi (Opened) tetikler — tarayıcı navigasyonu komut çağırır (aşağıda gerekçe). |
| IV. Result Pattern | ✅ | `FeatureObjectResultModel<T>` / `ResultDomain`; `MessageItem.Code` resource sabiti. |
| V. Kimlik + Açık Yetki | ✅ | Create/Cancel = `payment.write` (**geçici**, nihai platform IdP sonraki iş). Open/Pay = `AllowAnonymous` + gizli açılış token kapısı (fail-closed). |
| VI. Kart + Sır Minimizasyonu | ✅ | PAN yok; Pay stub. Dönüş DTO'su sır/credential içermez; açılış token'ı log/trace'te görünmez. |
| VII. Banka Port/Adapter | ✅ (kapsam dışı) | Sağlayıcı entegrasyonu stub; wire tipi üretilmez. |
| VIII. Spec-Driven | ✅ | spec → clarify → plan akışı izleniyor; Domain-TDD planlı. |
| IX. Domain Legibility | ✅ | `FLOW.md` aynı PR'da güncellenir (yeni Opened/Expired/Cancelled adımları + lazy expiry + cancel). Guard: `scripts/check-flow-links.sh`. |

**Sonuç**: İhlal yok. Complexity Tracking boş.

### GET-açılış-ucu durum geçişi (bilinçli CQRS sapması — gerekçe)

FR-006: müşteri linke dokunduğunda (tarayıcı GET) güven bağlamı gösterilmeli **ve** link Opened'a geçmeli (US2 AS-1). Saf CQRS'te GET salt-okurdur; ancak açılış bir domain olayıdır (Pending→Opened). Çözüm: `GET /payment-links/{openToken}` ucu `OpenPaymentLink` **command**'ını (`[Transactional]`) çağırır, dönüşte güven bağlamı HTML render eder. Komut idempotenttir (zaten Opened → no-op). Ayrı salt-okur query AÇILMAZ (tek yüzey, tek yazım yolu). Repository yok; handler `IDocumentSession`.

## Project Structure

### Documentation (this feature)

```text
specs/089-payment-link-creation/
├── plan.md              # bu dosya
├── research.md          # Faz 0 — karar günlüğü (partial unique, lazy expiry, grace, token, evrim)
├── data-model.md        # Faz 1 — PaymentLink alanları + durum makinesi + invariant'lar
├── quickstart.md        # Faz 1 — uçtan-uca doğrulama senaryoları (Aspire)
├── contracts/
│   └── pg-payment-link.md   # Faz 1 — 4 endpoint kontratı (istek/yanıt/hata/yetki)
├── checklists/
│   └── requirements.md  # (clarify'da doğrulandı, 16/16)
└── tasks.md             # /speckit-tasks çıktısı (bu komut ÜRETMEZ)
```

### Source Code (repository root)

```text
src/services/Payment.Api/
├── Domains/PaymentLinks/                         # HostedPayments/ bu dilimde buna evrilir
│   ├── PaymentLink.cs                            # aggregate (HostedPaymentSession.cs rename+evrim) + PaymentLinkStatus enum
│   ├── PaymentLinkEndpointExtension.cs           # 4 uç map + güven bağlamı HTML (BuildTrustContextPage)
│   └── Features/Commands/
│       ├── CreatePaymentLink.cs                  # US1 — store başlatma (idempotent aktif-tekil)
│       ├── OpenPaymentLink.cs                    # US2 — açılış token kapısı + Opened geçişi + güven bağlamı
│       ├── PayPaymentLink.cs                     # US2 — "Öde" stub → PROVIDER_UNAVAILABLE (lazy expiry+grace kontrol)
│       └── CancelPaymentLink.cs                  # US3 — store iptal → Cancelled
├── Options/PaymentLinkOptions.cs                 # HostedPaymentOptions evrim: Ttl, GraceWindow, LinkBaseUrl, güven-sayfa metinleri
├── Constants/PaymentLinkResourceConstants.cs     # HostedPaymentResourceConstants evrim (+ LINK_EXPIRED, LINK_CANCELLED/NOT_FOUND)
├── Program.cs                                    # Marten index: (MerchantId,OrderRef) PARTIAL unique (aktif durum) + OpenToken unique
└── FLOW.md                                       # domain süreci güncellenir (İlke IX, aynı PR)

tests/Payment.Api.Tests/
└── PaymentLinkTests.cs                           # HostedPaymentSessionTests evrim — Create/Open/Expire(lazy)/grace/Cancel/terminal
```

**Structure Decision**: Mevcut tek-BC düz yapı (conventions — `.csproj` BC kökünde). `HostedPayments/` slice'ı `PaymentLinks/`'e evrilir (rename + alan/durum genişletme); yeni teknik klasör açılmaz. Süreç-güdümlü `Process/` bu dilimde YOK (lazy expiry kararı; watchdog sonraki dilim).

## Complexity Tracking

> İhlal yok — boş.
