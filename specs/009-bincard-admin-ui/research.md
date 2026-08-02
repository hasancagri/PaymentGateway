# Phase 0 — Research: BinCard Katalog Admin UI (009)

Bilinmeyenler az; çoğu karar mevcut desenlerden (008 backend, 005 Admin UI) türer.

## R1 — Tekil detay ucu: mevcut `GET {bin}`'i zenginleştir vs yeni uç

**Decision**: Mevcut `GET api/v1/bin-cards/{bin}` yanıtını `CardInfo`'dan `BinCardDetailResponse`'a
(ham BinCard alanları: BinNumber/BankCode/CardType/CardBrand/CardProgram/Commercial + türetilmiş
`InstallmentBankCodes`) **zenginleştir**. Yeni ayrı uç açma.

**Rationale**: 008'de bu uç yalnız debug amaçlı; **iç HTTP tüketicisi yok** (ödeme/taksit yolu
`ResolveBinCard.Resolve` static'ini doğrudan çağırır). Yanıtı genişletmek hiçbir çağrananı kırmaz.
US1'in istediği marka/program/ticari `CardInfo`'da yok; detay DTO bunları ham document'ten taşır,
taksit-banka listesini `ResolveBinCard.DeriveInstallmentBankCodes` ile 008 paritesinde üretir (SC-003).

**Alternatives**: Ayrı `GET {bin}/detail` (iki uç aynı işi böler, gereksiz); Admin'in liste ucunu
`binNumber` ile tekil çağırıp taksit-bankayı ayrı resolve ile alması (iki tur + türetme Admin'e sızar).

## R2 — Liste sorgusu: filtre + sayfalama

**Decision**: Yeni `Queries/ListBinCards` — `GET api/v1/bin-cards?bankCode=&cardProgram=&cardType=
&cardBrand=&commercial=&page=&pageSize=`. Tüm filtreler opsiyonel, **AND** ile birleşir; verilmeyen
filtre atlanır. Marten `IQuerySession.Query<BinCard>()` üzerine koşullu `Where` + `ToPagedList(page,
pageSize)`. Yanıt `FeaturePagedResultModel<BinCardListItem>` (mevcut `PagedList.Core` altyapısı).

**Rationale**: Veri sahibi BC kendi sorgusunu sunar (anayasa I). `ToPagedList` Marten idiomatic;
`FeaturePagedResultModel` zaten Common'da. Sabit sunucu `pageSize` sınırı (aşırı istekte kırpılır) →
SC-002 (tam döküm yüklenmez). Filtreler enum ise sorgu enum eşitliği; `bankCode` exact eşleşme.

**Alternatives**: Offset yerine keyset sayfalama (~9957 kayıtta gereksiz karmaşıklık); Admin'in tümünü
çekip client-side filtrelemesi (anayasa I ihlali + transfer maliyeti) — reddedildi.

## R3 — Filtre alanları için indeksleme

**Decision**: İlk sürümde ek index yok; `CardProgram` zaten indexli (008). `BankCode` en sık filtre —
gerekirse `.Index(x => x.BankCode)` eklenir (küçük, non-breaking). ~9957 kayıtta filtreli sayfalama
index'siz de kabul edilebilir.

**Rationale**: Erken optimizasyon yapma; ölçek küçük. Index kararı ölçüm sonrası. Not olarak işaretli.

**Alternatives**: Tüm filtre alanlarını indexlemek (çoğu düşük kardinalite enum — index faydası düşük,
yazma maliyeti artar) — reddedildi.

## R4 — Enum'ları sınırda taşıma (Admin, Payment.Api enum tipine bağımlı olmasın)

**Decision**: Yanıt DTO'larında `CardType/CardBrand/CardProgram` **string ad** olarak döner
(`enum.ToString()`); liste filtreleri de string alınıp sunucuda `Enum.TryParse` ile çözülür (tanınmaz
→ o filtre yok sayılır / boş sonuç). Admin, string adı Türkçe etikete kendi eşler (sunum).

**Rationale**: Admin projesi Payment.Api'ye proje referansı vermez (BFF yalnız HTTP tüketir); enum
tipini paylaşmak sınır ihlali olur. String ad taşınabilir ve okunur (Newtonsoft int döndürme sorununu
da eller). Türkçe etiket **sunum** kararı — iş kuralı değil (FR-011 ihlali değil).

**Alternatives**: Enum int taşımak (Admin'de anlamsız sayı, ayrı legend gerekir); Newtonsoft global
`StringEnumConverter` (tüm Payment.Api yanıtlarını etkiler — dar kapsam yeğ) — reddedildi.

## R5 — Admin'in payment-api'yi tüketmesi

**Decision**: Yeni typed `IBinCardApiClient/BinCardApiClient : ApiClientBase`, BaseAddress
`http://payment-api` (service discovery). `Program.cs`'te `AddHttpClient` kaydı. AppHost'ta `admin-web`
node'una `.WithReference(payment-api).WaitFor(payment-api)` eklenir (şu an yalnız merchant/commission
referanslı). `SettlementAccountApiClient` deseniyle birebir.

**Rationale**: Mevcut Admin typed-client + service discovery deseni (Merchant/Commission). AppHost
referansı olmadan `http://payment-api` çözülmez. `ApiClientBase.SendAsync` transport hatasını dostça
sunucu hatasına çevirir (FR-012 karşılanır).

**Alternatives**: Gateway üzerinden çağırma (Admin doğrudan servis çağırıyor, mevcut desen); paylaşılan
client kütüphanesi (proje deseni her API'ye ayrı client) — reddedildi.

## R6 — İki ekran vs tek ekran

**Decision**: İki Razor sayfası `Pages/BinCards/`: `Resolve` (US1 tekil arama/detay) ve `Index` (US2
filtreli sayfalı liste). `_Layout`'a tek "BIN Kataloğu" menüsü (Index'e), Index'ten Resolve'a bağlantı.

**Rationale**: US1 ve US2 bağımsız test edilebilir dilimler (spec); ayrı sayfa her birini bağımsız
teslim edilebilir kılar (MVP = US1). Mevcut Admin sayfa-başına-görev deseniyle uyumlu.

**Alternatives**: Tek sayfada iki sekme (durum yönetimi karışır, bağımsız teslim zorlaşır) — reddedildi.

## R7 — Test kapsamı

**Decision**: Saf domain birim testi: `ListBinCards` filtre predikatı saf bir yardımcıya çıkarılabilir
(enum parse + hangi filtreler aktif) → test edilir; enum→TR etiket eşlemesi (Admin) saf → test
edilebilir ama Razor/Admin test edilmez (proje kuralı). Filtreli-sayfalı DB sorgusu + BFF akışı
**quickstart ile elle** (005 BFF smoke deseni).

**Rationale**: Anayasa/proje test kuralı: saf domain birim testi; HTTP/Razor/DB round-trip elle.
Filtre parse mantığı saf → regresyon değerli.

**Alternatives**: Admin entegrasyon testi (proje bilinçli ertelemesi — yok) — reddedildi.