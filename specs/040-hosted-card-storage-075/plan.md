# Implementation Plan: Hosted Kart Saklama — Store 075 Hizalama

**Branch**: `040-hosted-card-storage-075` | **Date**: 2026-09-12 | **Spec**: [spec.md](./spec.md)

## Summary

PG'nin kart yüzeyini store 075 sözleşmesine hizala: **hosted kart-ekleme** (iyzico Checkout Form —
PAN yalnız sağlayıcı sayfasında), `userHandle`(=cardUserKey)/`cardHandle`(=cardToken) ile canlı liste,
handle ile silme, handle ile **NON-3D taksitsiz** çekim. Eski PAN-POST `TokenizeCard` sökülür. Mevcut
iyzico V2 motoru (`Utils/*V2`), `StoredCard` document'i (cardUserKey/cardToken zaten tutuyor) ve saklı-
kartla çekim (033) yeniden kullanılır; eklenen = Checkout Form init/retrieve + card list + hosted-form
oturum korelasyonu (`CardSession`) + 075-şekilli yüzey.

## Technical Context

**Language/Version**: .NET (mevcut PG; `Nullable`+`ImplicitUsings` açık)
**Primary Dependencies**: Marten (paymentDb document store), Wolverine (in-proc + RabbitMQ), iyzico V2
wire (elle JSON+HMAC — `Utils/RestHttpClientV2`, `HashGeneratorV2`, `ProviderResourceV2`). Resmi SDK YOK.
**Storage**: paymentDb — `StoredCard` (sağlayıcı kimlikleri + gösterim; PAN yok) + yeni `CardSession`.
**Testing**: xUnit + Shouldly; saf domain birim testleri (iyzico HTTP test edilmez — sandbox canlı).
**Target Platform**: Linux/container (Aspire AppHost).
**Project Type**: Mikroservis (Payment BC).
**Performance Goals**: kart listeleme canlı sağlayıcı sorgusu < ~2 sn; çekim tek senkron istek.
**Constraints**: PAN/CVV PG sınırına HİÇ girmez (uyum, pazarlıksız); dış yüzey `pg-card-contract.md` ile
birebir; auth sandbox X-Api-Key; taksit yok (tek çekim); 3DS yok (NON-3D).
**Scale/Scope**: sandbox/demo; kullanıcı başına birkaç kart.

## Constitution Check

- **İlke I (BC izolasyonu):** ✅ Tüm iş Payment BC içinde; Merchant/Commission'a dokunmaz. Sağlayıcı
  (iyzico) sanksiyonlu dış entegrasyon (mevcut V2 motoru).
- **İlke II (zengin aggregate):** ✅ `CardSession` zengin (Start/Complete/Fail invariant'ları). `StoredCard`
  mevcut zengin document (cardUserKey/cardToken + gösterim).
- **İlke III (VSA/CQRS):** ✅ Yeni slice'lar `StoredCards/Features/{Commands,Queries}` (+ oturum). Endpoint
  Minimal API; handler `IDocumentSession` + iyzico motoru.
- **İlke IV (Result):** ✅ Handler `Feature*ResultModel`, aggregate `ResultDomain`; hata kodları resource sabiti.
- **İlke V (yetki):** ✅ Sandbox X-Api-Key merchant koruması (mevcut 039 deseni). Tam OAuth ertelenmiş.
- **İlke VI (Domain-TDD):** ✅ `CardSession` davranışı test-first.
- Callback ucu (hosted-form dönüşü) JWT'siz — conversationId/tek-kullanımlık oturum korelasyonu (store R3).

**Sonuç:** İhlal yok. Complexity Tracking gerekmez.

## Project Structure

### Documentation (this feature)

```
specs/040-hosted-card-storage-075/
├── plan.md · research.md · data-model.md · quickstart.md · contracts/ · tasks.md · checklists/
```

### Source Code (repository root)

```
src/services/Payment.Api/
├── Domains/StoredCards/
│   ├── StoredCard.cs                       # DEĞİŞİR: CardUserKey per-USER (grouping) + revoke by cardToken
│   ├── CardSession.cs                      # YENİ: hosted-form korelasyonu (aggregate)
│   ├── StoredCardEndpointExtension.cs      # DEĞİŞİR: 075 yüzeyi (card-sessions/list/handle-delete)
│   └── Features/
│       ├── Commands/StartCardSession.cs    # YENİ: iyzico Checkout Form init → addUrl + conversationId
│       ├── Commands/CompleteCardSession.cs # YENİ: CF retrieve → cardUserKey + StoredCard(lar) yaz
│       ├── Commands/DeleteCard.cs          # YENİ/UYARLA: userHandle+cardHandle → iyzico card delete
│       ├── Commands/TokenizeCard.cs        # SÖKÜLÜR (PAN-POST yolu)
│       ├── Commands/RevokeCard.cs          # SÖKÜLÜR/UYARLA (token→handle silme DeleteCard'a taşınır)
│       └── Queries/ListCards.cs            # YENİ: userHandle → iyzico card list → izdüşüm
├── Domains/Payments/Features/Commands/ChargePayment.cs  # DEĞİŞİR: handle girdisi + NON-3D + taksitsiz
└── Utils/                                  # UZATILIR: Checkout Form init/retrieve + Card list wire (V2 HMAC)
    ├── ProviderResourceV2.cs / ProviderConstants.cs     # + checkoutform/cardstorage uçları
    └── (RestHttpClientV2 / HashGeneratorV2 AYNEN)
```

**Structure Decision:** Kart işi mevcut `StoredCards` domain'inde toplanır (yeni BC/servis açılmaz —
god-service yasağı). iyzico motoru `Utils/` genişletilir (SDK yok; V2 HMAC deseni). Charge `Payments`
domain'inde kalır, girdi kimliği handle'a döner. Store yüzeyi `pg-card-contract.md` ile birebir.

## Açık tasarım noktaları (research.md'de çözülür)

- **cardUserKey gruplama (032 per-kart ertelemişti):** 075 "userHandle ile listele" için kullanıcı başına
  TEK cardUserKey gerekir. Çözüm: add-session'a **opsiyonel mevcut userHandle** (additive); varsa iyzico
  o cardUserKey'e ekler, yoksa yeni üretir. Store, `Wallet.PgUserHandle` doluysa gönderir (küçük store
  eklemesi; contract additive — eski çağrı kırılmaz).
- **Hosted-form dönüş biçimi:** push callback mü pull mu (store ikisini de destekliyor — pull yeterli).
- **CheckoutForm nominal doğrulama tutarı** (sandbox para hareketi yok) — canlı politika backlog.

## Complexity Tracking

> Anayasa ihlali yok — bu tablo boş.