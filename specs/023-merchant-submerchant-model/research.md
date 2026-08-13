# Research: Merchant SubMerchant Model (023)

Teknik bağlamda NEEDS CLARIFICATION kalmadı; aşağıdaki kararlar spec'teki serbestlik
alanlarını kapatır. Kaynaklar: 022 iyzico malzemesi (`Domains/SubMerchants/*`), iyzico
SubMerchant sözleşmesi (020 SDK incelemesi), 012 kararları (Shared event'ler +
`MerchantClientEventHandler`), 004/006/014/015 emsalleri.

## R1 — Alan seti: iyzico sözleşmesinden hangi alanlar alınır

- **Decision**: Aggregate alanları: `Name, Email, GsmNumber, Address, Iban, ContactName,
  ContactSurname, Type (MerchantType), IdentityNumber?, TaxOffice?, TaxNumber?,
  LegalCompanyTitle?, SubMerchantKey? (bu fazda hep null), Status, MerchantKey`.
  **Alınmayanlar**: `Currency` (anayasa: yalnız TL — çok-değerli modellenmez),
  `SwiftCode` (yurtdışı havale — YAGNI), `SettlementDescriptionTemplate` (YAGNI),
  `SubMerchantExternalId` (saklanmaz — entegrasyon fazında merchant kimliğinin kendisi
  gönderilir, ayrı alan gerekmez).
- **Rationale**: FR-001 "bu setin dışında alan eklenmez" der ve seti sayar; iyzico
  `CreateSubMerchantRequest`'teki kalan alanlar sözleşmeyi bozmadan entegrasyon anında
  sabit/türetilmiş değerle doldurulabilir.
- **Alternatives considered**: Tüm iyzico alanlarını birebir taşımak — reddedildi (YAGNI,
  TL-only anayasa ihlali `Currency` için); ayrı `SubMerchantExternalId` saklamak —
  reddedildi (kimlik eşlemesi = merchant Id, spec varsayımı).

## R2 — İşyeri tipi ve tip-uyum kuralları

- **Decision**: Domain enum `MerchantType { Personal, PrivateCompany,
  LimitedOrJointStockCompany }` (sağlayıcı `SubMerchantType` string'i BC dışına/içine
  sızmaz; eşleme ileriki entegrasyon işinde). Zorunlu alan matrisi (iyzico sözleşmesi):
  - `Personal` → `IdentityNumber` zorunlu; vergi alanları serbest (boş olabilir).
  - `PrivateCompany` (şahıs şirketi) → `IdentityNumber + TaxOffice + LegalCompanyTitle`
    zorunlu; `TaxNumber` opsiyonel.
  - `LimitedOrJointStockCompany` (sermaye şirketi) → `TaxOffice + TaxNumber +
    LegalCompanyTitle` zorunlu.
  Fazla dolu alan REDDEDİLMEZ (yalnız zorunluluk denetlenir — YAGNI).
- **Rationale**: iyzico onboarding sözleşmesinin zorunluluk matrisi budur; spec senaryo 2
  (şahıs vergi alansız geçer, sermaye şirketi unvan/vergisiz reddedilir) birebir karşılanır.
- **Alternatives considered**: Tip başına ayrı aggregate/VO hiyerarşisi — reddedildi
  (YAGNI, tek aggregate + matris yeter); fazla alanları reddetmek — reddedildi (spec
  istemez, sıkılaştırma entegrasyonda değerlendirilir).

## R3 — Doğrulama biçimleri (IBAN, e-posta) ve yeri

- **Decision**: IBAN: TR-IBAN mod-97 denetimi (TR + 24 hane, ISO 7064 — 004 emsali).
  E-posta: basit biçim denetimi (tek `@`, yerel+alan parça boş değil). GSM: biçim
  denetimi YOK (spec FR-002 saymaz — YAGNI). Doğrulama mantığı aggregate'in `Create` ve
  `UpdateDetails` metotlarında **inline** yaşar (015: private helper yasak, kod tekrarı
  bilinçli; VO açılmaz — tek kullanım alanı, YAGNI).
- **Rationale**: FR-002 IBAN + e-posta biçimini sayar; 015 kuralı helper'a izin vermez;
  iki metotta tekrar bilinçli kabul (kural açıkça böyle diyor).
- **Alternatives considered**: `Iban`/`Email` ValueObject (VO'da helper serbest) —
  reddedildi (YAGNI: tek aggregate kullanıyor; 016'da tek-örnek VO'nun silinme emsali
  var); DataAnnotations — reddedildi (invariant aggregate'te yaşar, anayasa II).

## R4 — MerchantKey üretimi ve sır hijyeni

- **Decision**: `Create` fabrikasında `"mk_" + Guid` üretilir (006 emsal biçimi);
  immutable — hiçbir davranış metodu değiştirmez. Yalnız `CreateMerchant.Response`
  taşır; `GetMerchant`/`ListMerchants` yanıt tiplerinde alan hiç YOK (serileşme riski
  sıfır). Event tarafında yalnız `MerchantCreated` taşır (mevcut sözleşme; Identity'ye
  istemci sırrı olarak gider). Rotasyon/kayıp anahtar kapsam dışı (spec).
- **Rationale**: FR-003 + SC-004; 012 kararı MerchantKey = client_secret, yalnız
  `connect/token`'a gider.
- **Alternatives considered**: Ayrı kriptografik rasgele sır — reddedildi (006 emsali
  mk_+Guid, dev fazı yeterli; rotasyon işi gelince değerlendirilir).

## R5 — Statü makinesi ve varsayılan statü

- **Decision**: `MerchantStatus { Active, Passive, Suspended }`; yeni merchant **Active**
  doğar (spec varsayımı — onboarding/Provisioning zinciri 013'le söküldü). Üç statü
  arasında serbest operatör geçişi; aynı statüye geçiş **idempotent başarı** (hata değil)
  ve bu durumda event YAYINLANMAZ (değişiklik yok → duyuru yok; kimlik senkronu zaten
  idempotent, gereksiz trafik üretilmez). `ChangeStatus` `ResultDomain<bool>` benzeri
  "değişti mi" bilgisini döner ki handler yayını koşullayabilsin.
- **Rationale**: Spec edge case "idempotent kabul edilir"; FR-005 atomiklik yalnız gerçek
  değişiklik duyurusunu gerektirir.
- **Alternatives considered**: Aynı-statüde de yayınlamak — reddedildi (gereksiz;
  handler'da koşul basit); geçiş kısıtları (ör. Suspended→Active yasak) — reddedildi
  (spec tanımlamaz, operatör serbestisi).

## R6 — Identity zinciri: hangi event'ler, hangi tüketici

- **Decision**: Mevcut `Shared.IntegrationEvents.MerchantCreated(MerchantId, MerchantKey,
  Status)` ve `MerchantStatusChanged(MerchantId, NewStatus)` AYNEN kullanılır; statü
  string'i enum `ToString()` ("Active"/"Passive"/"Suspended" — handler
  OrdinalIgnoreCase karşılaştırır). `MerchantProvisioned` bu fazda YAYINLANMAZ (varsayılan
  Active — Provisioning statüsü yok). Identity.Server `MerchantClientEventHandler` ve
  Program.cs yayın kayıtları (exchange `merchant.lifecycle`) DEĞİŞMEZ — Merchant.Api
  Program.cs'te kayıtlar zaten duruyor. Yayın `[Transactional]` handler'dan
  `IMessageBus.PublishAsync` ile → Marten commit'iyle atomik (outbox; FR-005).
- **Rationale**: Spec FR-005 "mevcut sözleşme ve tüketici aynen"; 012 kanıtlı düzlem.
- **Alternatives considered**: Yeni event tipi — reddedildi (sözleşme değişikliği
  gereksiz); senkron HTTP senkronizasyonu — reddedildi (anayasa I: event tabanlı).

## R7 — Uç yüzeyi ve policy eşlemesi

- **Decision**: 5 uç, hepsi açık policy beyanlı (011 kuralı + 012 düzlem policy'leri):

  | Uç | Scope | Düzlem policy |
  |----|-------|---------------|
  | `POST /api/v1/merchants` | `merchant.write` | `AdminPlaneOnly` |
  | `PUT /api/v1/merchants/{merchantId}` | `merchant.write` | `AdminPlaneOnly` |
  | `PUT /api/v1/merchants/{merchantId}/status` | `merchant.write` | `AdminPlaneOnly` |
  | `GET /api/v1/merchants/{merchantId}` | `merchant.read` | `MerchantScoped` |
  | `GET /api/v1/merchants` | `merchant.read` | `AdminPlaneOnly` |

  Tekil GET `MerchantScoped`: merchant kendi token'ıyla (merchant_id claim = route)
  yalnız kendi kaydını okur; claim'siz admin token'ı her kaydı okur. Liste
  `AdminPlaneOnly`: merchant token'ı tüm listeyi göremez (tenant sınırı).
- **Rationale**: FR-004 + spec varsayımı ("merchant-self yüzeyi güncelleme dahil admin
  düzleminde; yalnız kendi kaydını okuma merchant'a açık"); statü ucu US2 senaryo 4.
- **Alternatives considered**: Liste ucuna MerchantScoped + filtre — reddedildi (spec
  merchant'a liste vermez); DELETE ucu — reddedildi (spec: silme yok, statü yeter).

## R8 — Test projesi kurulumu

- **Decision**: `tests/Merchant.Api.Tests` — 022'de silinen csproj deseni birebir:
  net10.0, `IsTestProject`, xUnit + Microsoft.NET.Test.Sdk + xunit.runner.visualstudio
  (hepsi CPM'de mevcut — `Directory.Packages.props` DEĞİŞMEZ), tek ProjectReference
  Merchant.Api. Kapsam: yalnız aggregate davranışları (tipbaşına oluşturma, tip-uyum
  ihlalleri, IBAN/e-posta redleri, statü geçişleri + idempotent geçiş, MerchantKey
  biçimi/değişmezliği, UpdateDetails kimlik+key korunumu). DB/HTTP yok (anayasa test
  kuralı). `PaymentGateway.slnx`'e eklenir.
- **Rationale**: FR-007/SC-005; silinen projenin deseni kanıtlı ve CPM uyumlu.
- **Alternatives considered**: NUnit (020 SDK emsali) — reddedildi (BC test emsali xUnit;
  020 istisnası SDK'ya özeldi ve silindi); handler/entegrasyon testi — reddedildi
  (anayasa: saf domain, canlı doğrulama quickstart'la).

## R9 — Marten kayıt modeli

- **Decision**: Aggregate doğrudan Marten dokümanı (mevcut konvansiyon: Newtonsoft +
  `NonPublicSetters` + non-public ctor — Program.cs'te hazır). Ayrı read model YOK
  (liste/tekil sorgular `IDocumentSession.Query<Merchant>()` üstünden; MerchantKey'i
  yanıt tipine projekte etmeyerek sızma engellenir). Şema/DB kaydı Program.cs'te hazır
  (`merchantDb` + `MerchantSchemaName`) — dokunulmaz.
- **Rationale**: Mevcut altyapı yeter; read model 010 deseninin gerekçesi (cross-BC
  besleme) burada yok.
- **Alternatives considered**: Ayrı list-item read model — reddedildi (YAGNI; response
  record'u projeksiyon görevi görür).
