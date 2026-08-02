# Phase 0 — Research: BinCard Referans Kataloğu

Çoğu karar brainstorm'da (2026-08-02) verildi; burada Decision / Rationale / Alternatives olarak
sabitlenir. NEEDS CLARIFICATION yok.

## R1 — Yerleşim ve model şekli

**Decision**: BinCard, Payment BC'de (`Domains/BinCards/`) bir **Marten document** (referans/lookup),
davranış-zengin aggregate DEĞİL. Kimlik = `BinNumber` (string).

**Rationale**: BIN çözümü her ödeme/quote'ta hot-path — ayrı BC senkron cross-BC gerektirirdi
(anayasa I caydırır). Veri referans/lookup (okuma-ağır, yazma-nadir-toplu, invariant yok); aggregate
sahte davranış getirir (anayasa II'nin amacı iş kuralını modellemek — BinCard'ın iş kuralı yok).
Mevcut "yerel BankCatalog kopyası" (MerchantSettlementAccount) deseniyle uyumlu.

**Alternatives**: Zengin aggregate (YAGNI); Commission BC (kart metadata onun işi değil); yeni
referans BC (hot-path senkron zorlar).

## R2 — Enum bağlama ve CP.VPOS sınırı

**Decision**: `CardType{Debit,Credit}`, `CardBrand{Unknown,Visa,MasterCard,Troy,Amex,Discover,
Unionpay,JCB}`, `CardProgram{Unknown,Axess,Bank24,Bankkart,Bonus,CardFinans,Maximum,MilesAndSmiles,
Neo,Paraf,ShopAndFly,Wings,World,Advantage,SaglamKart}` — Payment domain'de **düz C# enum** (mevcut
`PaymentStatus` stili). CP.VPOS `CreditCardType/Brand/Program` (veya import int değerleri) yalnız
**seed/import sınırında** domain enum'una çevrilir (`BinCardMapping`).

**Rationale**: Anayasa CP.VPOS kuralı — CP.VPOS tipleri slice/domain sınırını geçmez. Tip-güvenlik
routing/quote'u okunur kılar (`card.CardType == CardType.Credit`). Enum değerleri legend'la birebir
(CP.VPOS enum int değerleri korunur → parite).

**Alternatives**: Ham int saklamak (tip-güvenlik yok, anayasa II'ye ters); `Enumeration` base
(proje Payment BC'de düz enum kullanıyor — tutarlılık için düz enum).

## R3 — Çözümleme sözleşmesi ve bilinmeyen-BIN

**Decision**: `ResolveBinCard(binNumber) → CardInfo?`. Bulunamazsa **null** (istisna yok, sahte
`CardInfo` üretilmez). 8 haneli girdi: önce tam eşleşme, yoksa ilk 6 (`binNumber[..6]`). `CardInfo`
= mevcut `record CardInfo(string? BankCode, bool IsCreditCard, IReadOnlyList<string> InstallmentBankCodes)`.

**Rationale**: Kullanıcı kararı — bugünkü `LoadCardInfo` sessizce boş `CardInfo(null,false,[])`'e
degrade ediyor; yanıltıcı. Null, çağıranı bilinçli ele almaya zorlar (anayasa IV: beklenen "yok"
istisnasız taşınır). 8→6 fallback CP.VPOS davranış paritesi.

**Alternatives**: `Result<CardInfo>` (null yeterli, daha yalın); sahte-default korumak (kullanıcı
reddetti); istisna fırlatmak (beklenen durum, anayasa IV ihlali).

## R4 — InstallmentBankCodes türetme

**Decision**: Çözüm anında türet (saklama). Kredi kartı + geçerli program için: katalogdan aynı
`CardProgram`'a sahip kayıtları çek, `BankCode`'a göre grupla (destek sayısına göre azalan), kod
listesi yap; kartın kendi bankası listenin **başına** alınır. Banka kartı/bilinmeyen program → boş.
CP.VPOS `CreditCardBinQuery`'deki mantığın aynısı.

**Rationale**: Parite (SC-001) — CP.VPOS ile birebir aynı sonuç. Türetme normalize veriyi korur
(banka listesini her kayıtta tekrarlamaz). `CardProgram` index'i sorguyu ucuzlatır.

**Alternatives**: Türetilmiş listeyi kayıtta saklamak (denormalize; import'ta tutarlılık derdi);
her program için materialized view (erken optimizasyon — gerekirse cache).

**Caveat**: Sıcak yolda ekstra bir indexli sorgu; ~9900 kayıt için kabul. Gerekirse `HybridCache`
(mevcut paket) ile program→bankalar cache'lenir.

## R5 — Seed (Story 2)

**Decision** (uygulamada güncellendi — kullanıcı kararı): `BinCardSeeder : Marten.Schema.IInitialData`.
`Populate` startup'ta çalışır; katalog boşsa **gömülü `Domains/BinCards/Data/bincards.json`**
(EmbeddedResource, ~9957 kayıt) deserialize edilir, her kayıt `BinCardMapping.FromCodes` ile domain'e
çevrilir, toplu `session.Store`. Doluysa hiçbir şey yapmaz (idempotent). Kayıt Marten 9 fluent
`.InitializeWith(new BinCardSeeder())` (not: `opts.InitialData` üyesi Marten 9'da yok).

**Rationale**: Kullanıcı seed kaynağını `VPOSClient.AllCreditCardBinList()` yerine JSON istedi —
böylece CP.VPOS'a **seed bağımlılığı da kalkar** (BIN verisi tamamen Payment BC'ye ait, donmuş
kütüphaneden kopuk). JSON, CP.VPOS `BinService.data` literalinden bir kez çıkarıldı (`""`→`"` unescape).

**Alternatives**: `VPOSClient.AllCreditCardBinList()` (ilk karar — CP.VPOS'a seed bağımlılığı
bırakıyordu, kullanıcı reddetti); `IHostedService`/`BackgroundService` (Marten `IInitialData` daha idiomatic).

## R6 — Import (Story 3, idempotent upsert)

**Decision**: `Commands/ImportBinCards(list)` — her kayıt `BinCardMapping` ile domain'e çevrilir,
`session.Store` ile upsert (kimlik `BinNumber` → var olan güncellenir, yeni eklenir). Geçersiz/eksik
kayıt atlanır ve sonuç raporunda sayılır (`FeatureObjectResultModel`). Endpoint `POST api/v1/bin-cards/import`.

**Rationale**: Marten `Store` upsert semantiği idempotency'yi doğal verir (aynı liste → aynı sonuç,
SC-004). Yeniden derleme/deploy gerekmez (FR-011). Geçersiz kayıt toleransı (FR-010) batch'i korur.

**Alternatives**: Delete-all + insert (idempotent değil, downtime/riski); per-kayıt CRUD (elle,
~9900 için pratik değil — reddedildi).

## R7 — Okuma yolu geçişi

**Decision**: `ProcessPayment.LoadCardInfo` ve `GetInstallmentOptions` `VPOSClient.CreditCardBin
Query` yerine `ResolveBinCard` çağırır (`IMessageBus.InvokeAsync` veya doğrudan query handler).
`CardInfo?` null ise: `ProcessPayment` → Result reddi; `GetInstallmentOptions` → boş/uygun sonuç.
CP.VPOS değiştirilmez, sadece BIN için çağrılmaz.

**Rationale**: FR-012. Minimal cerrahi swap; derin ProcessPayment yeniden kurgusu ayrı feature.
Null ele alma R3 kararının uygulaması.

**Caveat**: Bu, `LoadCardInfo` imzasını `CardInfo?` yapar → iki çağıran güncellenir. `GetInstallment
Options`'ın mevcut Model B tutar davranışı 008'de değişmez (o 007'nin işi).

## R8 — Test stratejisi

**Decision**: `tests/Payment.Api.Tests` saf domain: `BinCardMapping` (CP.VPOS enum/int → domain enum,
tüm değerler + Unknown), 8→6 fallback seçimi, `InstallmentBankCodes` türetme (kart bankası başta,
banka kartı → boş), import upsert idempotency (aynı kayıt iki kez → tek kayıt), bilinmeyen BIN → null.
Seed/import DB round-trip ve HTTP birim testi yok — quickstart elle. Anayasa test kuralı.