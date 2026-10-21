# Implementation Plan: NonSecure Banka Çekim Dilimi

**Branch**: `090-nonsecure-bank-charge` | **Date**: 2026-10-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/090-nonsecure-bank-charge/spec.md`

## Summary

PaymentLink "Öde" stub'ı (`PROVIDER_UNAVAILABLE`) gerçek NonSecure banka çekimiyle değişir. Müşteri hosted sayfada kart girer → BIN'den issuer/şema/kart-tipi çözülür → router iki aşamada (yetenek eleme → on-us + maliyet sıralaması) 3 simüle banka arasından aday sıralar → çekim banka başına adapter (İlke VII port/adapter, wire nested, anti-corruption) arkasından sıradaki bankaya gider → sonuç normalize (onay / 4-sınıf red) → TechnicalFailure/ConfigError'da sıradaki uygun bankaya **canlı failover**, Issuer/Fraud'da durur → onayda link `Paid`, her deneme `PaymentAttempt` izlenir → terminal sonuç store'a HMAC-imzalı dayanıklı webhook + `PaymentCompleted/Failed` olayı. BinCard (BIN→kart bilgisi) statik seed'den gelir. Kart verisi hiçbir kalıcı ortamda tutulmaz (İlke VI). Çift-çekim: durum-sorgu YOK → failover penceresi açık ama simülatörde bakiye olmadığından zararsız; gerçek banka öncesi durum-sorgu zorunlu (sonraki versiyon).

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable`+`ImplicitUsings` açık)

**Primary Dependencies**: Wolverine (bus + RabbitMQ fanout + `[Transactional]` outbox), Marten (Postgres doküman), Scrutor (DI marker), Minimal API, `System.Security.Cryptography.HMACSHA256`

**Storage**: Marten/Postgres (`PaymentSchemaName`). `PaymentLink` düz doküman (089); `PaymentAttempt` link altında nested koleksiyon. Kart verisi (PAN/CVV) HİÇBİR kalıcı ortamda yok (İlke VI).

**Testing**: xUnit, saf domain birim testleri (host/entegrasyon harness yok). Domain-TDD: aggregate davranışı + router kararı test-first; adapter/handler/endpoint test-sonra/canlı.

**Target Platform**: Linux/container, .NET Aspire AppHost (Postgres + RabbitMQ); tek servis izole çalışmaz.

**Project Type**: Tek BC web-service (`src/services/Payment.Api`) + test projesi.

**Performance Goals**: Senkron çekim müşteri-algısı anlık (hosted sayfa "Öde" → sonuç tek istekte). Ölçek N/A (dev/sim).

**Constraints**: Yalnız TRY. PAN PG sunucusunda transient (saklanmaz/loglanmaz/outbox'a girmez). Sır (CallbackSecret) yalnız secret-store. Çift-çekim: durum-sorgu olmadığından 2. CANLI bankaya güvenli failover sonraki versiyon (bu versiyon tek canlı banka → pencere latent).

**Scale/Scope**: 1 yeni child entity (PaymentAttempt) + BinCard referans doküman + seed + port + 3 simüle adapter + 2-aşamalı router (domain-service: yetenek eleme + on-us/maliyet + failover) + handler yeniden-yazımı + webhook mesaj/handler + hosted sayfa kart formu + Options (BankRouting/Simulator×3/StoreCallback/PosAccount) + BinCard lookup + testler + FLOW.md. (Büyüdü: akıllı routing + 3 banka.)

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden denetlenir.*

- **I. BC İzolasyonu** ✅ Tek Payment BC; banka = BC değil, adapter arkası dış sistem; wire domain sınırını geçmez. Store bildirimi `Shared` integration event + imzalı webhook (DB erişimi değil). **GEÇER.**
- **II. Zengin Domain** ✅ `PaymentAttempt` child entity (AggregateRoot değil, base almaz); koleksiyon private + `IReadOnlyList`; mutasyon yalnız `PaymentLink.RecordAttempt`; `BankChargeOutcome` VO (private ctor + static factory); `DeclineClass`/`PaymentModel` enum aggregate dosyasında. **GEÇER.**
- **III. VSA + CQRS** ✅ `PayPaymentLink` = Commands slice, `[Transactional]`, `IDocumentSession`+`IMessageBus`. `BankChargeRouter` = domain-service (kullanıcı-tetikli senkron "Öde" → Process/Saga değil). Repository yok. **GEÇER.**
- **IV. Result** ✅ Handler `FeatureResultModel`, aggregate `ResultDomain`; red `MessageItem.Code` resource sabiti; banka sonucu outcome-enum (`BankChargeOutcome`, retry-able Failed ≠ `Error`). **GEÇER.**
- **V. Yetki** ✅ `/payment-links` + cancel = `payment.write` (geçici, 089 devamı); müşteri açılış/ödeme ucu anonim + gizli-token (fail-closed). PG MCP yüzeyi YOK. Platform IdP sonraki versiyon (Assumptions). **GEÇER.**
- **VI. Kart/Sır Minimizasyonu (NON-NEGOTIABLE)** ✅ PAN transient (command `InvokeAsync`, `PublishAsync` DEĞİL → outbox'a girmez); aggregate PAN taşımaz; `PaymentAttempt` yalnız maskeli/ref/authCode; CallbackSecret secret-store, log/trace/DTO'da yok. **GEÇER.**
- **VII. Port/Adapter + Anti-Corruption** ✅ Ortak port `IBankPaymentAdapter` (NonSecure sale; iptal/iade/durum ileride); her adapter wire'ını private nested tutar + normalize `BankChargeOutcome`'a çevirir; paylaşılan SDK yok. Routing İlke VII 2-aşama **bu versiyonda canlı**: yetenek eleme → on-us (BinCard issuer == PosAccount banka) + maliyet; PosAccount yetenek/fiyat = PG öngörü kopyası (banka otorite). timeout "başarısız değil" ayrı sınıf (TechnicalFailure); failover canlı ama timeout-güvenli failover (durum-sorgu) sonraki versiyon. **GEÇER.**
- **VIII. Spec-Driven** ✅ Tam akış, domain-TDD. **GEÇER.**
- **IX. Domain Legibility** ✅ FLOW.md aynı PR'da güncellenir (yeni adım: çekim/failover/Paid/webhook; yeni edge-anchor tipleri). **GEÇER.**

**Sonuç: tüm kapılar geçer, gerekçelendirilecek ihlal yok → Complexity Tracking boş.**

## Project Structure

### Documentation (this feature)

```text
specs/090-nonsecure-bank-charge/
├── spec.md
├── plan.md              # bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/           # Phase 1 (port + webhook + /pay sözleşmeleri)
└── checklists/requirements.md
```

### Source Code (repository root)

```text
src/services/Payment.Api/
├── Domains/PaymentLinks/
│   ├── PaymentLink.cs                      # DEĞİŞİR: RecordAttempt + Paid + PaymentModel/DeclineClass enum + PayableGuard helper + _attempts
│   ├── PaymentAttempt.cs                    # YENİ: child entity (AggregateRoot değil)
│   ├── ValueObjects/BankChargeOutcome.cs    # YENİ: normalize sonuç VO
│   ├── BankCharge/
│   │   ├── IBankPaymentAdapter.cs           # YENİ: port + BankChargeRequest/CardData transient input
│   │   ├── SimulatorBankAdapter.cs          # YENİ: in-process fake (3 örnek config'le), private nested wire + ResponseMapper
│   │   ├── BankChargeRouter.cs              # YENİ: domain-service, 2-aşama (yetenek eleme + on-us/maliyet) + failover
│   │   └── RoutingCandidate.cs              # YENİ (ops.): aday sıralama VO (banka + on-us + maliyet)
│   ├── Features/Commands/
│   │   ├── PayPaymentLink.cs                # DEĞİŞİR: kart VO + BIN çözüm + router + Paid/decline + publish
│   │   └── StoreResultDelivery.cs           # YENİ: HMAC imzalı durable webhook (041 yeniden)
│   └── PaymentLinkEndpointExtension.cs      # DEĞİŞİR: kart formu + /pay body bağlama
├── Domains/BinCards/                        # YENİ: BinCard referans doküman BC-içi
│   ├── BinCard.cs                            # YENİ: BIN→issuer banka/şema/kart tipi (düz doküman)
│   ├── BinCardSeeder.cs                      # YENİ: statik embedded JSON seed (startup)
│   └── bin-seed.json                         # YENİ: küçük sim BIN seti (embedded resource)
├── Options/
│   ├── BankRoutingOptions.cs                # YENİ: OrderedBankCodes + health seam
│   ├── PosAccountOptions.cs                 # YENİ: acquirer öngörü (bankCode + yetenek + maliyet/öncelik) ×3
│   ├── SimulatorBankOptions.cs              # YENİ: BankCode + test-veri sentinel (banka başına örnek)
│   └── StoreCallbackOptions.cs              # YENİ: CallbackUrl(appsettings) + CallbackSecret(secret-store)
├── Constants/PaymentLinkResourceConstants.cs # DEĞİŞİR: red taksonomi kodları
├── Program.cs                                # DEĞİŞİR: Options + BinCard seed + 3 adapter DI + gerekirse Discovery.IncludeType
└── appsettings.json                          # DEĞİŞİR: BankRouting/PosAccount/Simulator section

tests/Payment.Api.Tests/
├── PaymentLinkTests.cs                       # DEĞİŞİR: RecordAttempt/Paid/terminal/çift-çekim
├── BankChargeRouterTests.cs                  # YENİ: 2-aşama routing (eleme + on-us/maliyet) + failover + taksonomi map
└── BinCardTests.cs                           # YENİ (ops.): BIN çözüm + bilinmeyen BIN fail-open
```

**Structure Decision**: Mevcut tek-BC düz yapı (089). Yeni aggregate YOK; `PaymentLink` genişler. Banka port+adapter+router, İlke VII gereği kullanan slice'ın içinde (`Domains/PaymentLinks/BankCharge/`), wire private nested. **BinCard = BC-içi referans doküman** (`Domains/BinCards/`, aggregate değil salt-okur referans; conventions.md "fanout'tan beslenen salt-okur referans belgesi" Domains'te kalabilir). Süreç-güdümlü `Process/`/`Saga/` YOK (çekim senkron kullanıcı-tetikli). PosAccount öngörü verisi `Options/` (banka otorite, PG kopyası — İlke VII).

## Complexity Tracking

> Constitution Check ihlalsiz geçti — bu bölüm boş.
