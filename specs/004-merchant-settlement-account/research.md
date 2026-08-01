# Research: Merchant Settlement Hesabı (004)

Phase 0 — teknik bağlam netleştirme. Tüm kararlar spec + anayasa + mevcut kod desenleriyle
hizalı. NEEDS CLARIFICATION kalmadı.

## D1 — Banka referansı nasıl doğrulanır (BC izolasyonu)

**Decision**: Settlement hesabı bankayı **4-hane banka kodu** (`BankCode`) olarak tutar. Kod,
Merchant BC'nin kendi tuttuğu **yerel `BankCatalog` kopyasına** (statik referans veri, gömülü
singleton lookup) karşı bellekte doğrulanır. Commission BC'ye runtime çağrı yapılmaz.

**Rationale**:
- Anayasa I: bir servis başka servisin DB/aggregate'ine erişemez. Commission.Api'daki `Bank`
  aggregate (Guid kimlikli) Merchant BC'den okunamaz.
- Settlement hesabının ihtiyacı "geçerli banka kodu mu?" — bu **katalog** sorusu, Commission'daki
  `Bank` **kaydı** sorusu değil (o komisyon grid'i için ayrı concern).
- Katalog statik + nadir değişir; CreateMerchant'ın country/city/mcc gömülü lookup deseniyle
  birebir aynı. `BankCatalog` zaten CP.VPOS'tan statik kopyalanmış — üçüncü kopya aynı pratik.

**Alternatives considered**:
- **gRPC ile Commission.Api'ye sor**: runtime bağımlılık + her doğrulamada ağ + Commission down
  ise Merchant çalışmaz. Anlık tutarlılık şartı yok → gereksiz. Red.
- **Ayrı Reference BC (tek kaynak)**: doğru "single source of truth" ama yeni servis + haberleşme
  altyapısı; 004 için fazla. Katalog yönetimi büyürse gelecekte terfi yolu. Ertelendi.

**Bedeli (kabul edildi)**: Katalog iki serviste (Commission.Api + Merchant.Api) elle senkron
tutulur. Yeni banka nadir; drift riski tolere edilebilir. Kullanıcı A yolunu onayladı.

## D2 — IBAN doğrulama yeri ve yöntemi

**Decision**: IBAN doğrulaması saf **aggregate içinde** (statik `Create` + `UpdateDetails`).
İki katman: (a) TR format — `^TR\d{24}$` (26 karakter, boşluklar temizlenip büyük harfe çevrilir);
(b) ISO 13616 **mod-97 checksum** (== 1). Geçersizse `ResultDomain` hata (`COMMON_MESSAGE_INVALID_FORMAT`).

**Rationale**:
- Anayasa II: invariant handler'da değil aggregate'te. IBAN geçerliliği hesabın invariant'ı.
- Anayasa IV + test önceliği: saf, deterministik, host'suz birim testi kolay (mod-97 kritik test).
- Yalnız TL/TR kısıtı → yalnız `TR` ülke kodu kabul; başka ülke IBAN'ı format hatası.

**Alternatives considered**:
- Sadece regex (mod-97 yok): tipografik hatayı yakalamaz; parayı yanlış hesaba gönderme riski.
  Mod-97 ucuz + saf → dahil edildi.
- Doğrulamayı handler'da yapmak: anayasa II ihlali. Red.

## D3 — Persistence ve slice yerleşimi

**Decision**: `MerchantSettlementAccount` yeni Marten document; `Program.cs`'te
`opts.Schema.For<MerchantSettlementAccount>()` eklenir (`MerchantSchemaName` şeması). Yeni slice
`Domains/MerchantSettlementAccounts/` altında; mevcut `Merchants` slice'ına dokunulmaz. Handler'lar
`IDocumentSession` alır, repository yok, command'lar `[Transactional]`.

**Rationale**: Anayasa III + mevcut `Merchants` deseni birebir. Ayrı document = ayrı tutarlılık
sınırı; Merchant aggregate şişmez.

**Alternatives considered**:
- Hesapları `Merchant` aggregate'i içine koleksiyon olarak gömmek: mevcut aggregate'i değiştirir
  (spec FR-013 hariç tutuyor), tutarlılık sınırını gereksiz büyütür, liste sorgusunu zorlaştırır. Red.

## D4 — Merchant varlığı ve mükerrer IBAN kontrolü

**Decision**: Handler'da Marten sorgusuyla: (a) `Merchant` var mı (MerchantId ile), yoksa
`COMMON_MESSAGE_RECORD_NOT_FOUND`; (b) aynı merchant içinde aynı normalize IBAN var mı, varsa
`COMMON_MESSAGE_RECORD_DUPLICATE`. Aggregate saf olduğundan bu iki kontrol (referans + benzersizlik)
handler sorumluluğu — CreateMerchant'ın lookup-in-handler deseniyle aynı.

**Rationale**: Anayasa II: aggregate başka aggregate'i sorgulamaz; çapraz varlık/benzersizlik
handler'da. Mesaj kodları Common'da mevcut (yeni resource dosyası gerekmez).

**Alternatives considered**: DB unique index ile mükerrerlik: Marten document'ta merchant-scoped
composite unique kurmak mümkün ama handler kontrolü mesajı Result pattern'le döndürür (anayasa IV);
index savunma katmanı olarak sonradan eklenebilir. Şimdilik handler kontrolü yeterli.

## D5 — Durum modeli

**Decision**: `SettlementAccountStatus` düz enum `{ Active = 1, Passive = 2 }`. Oluşturmada Active;
`Activate()`/`Deactivate()` davranış metotları. Silme yok (soft). `AggregateRoot.IsActive`/`IsDeleted`
alanlarıyla hizalanır (mevcut Merchant deseni: `Deactivate` hem Status hem `IsActive` günceller).

**Rationale**: Mevcut `MerchantStatus` konvansiyonu (düz enum, kullanıcı direktifi). Suspended
gerekmez — settlement hesabı için aktif/pasif yeterli (YAGNI).

**Alternatives considered**: `Enumeration` smart-enum: mevcut konvansiyon düz enum; tutarlılık için
düz enum. Red (şimdilik).

## Çözülen bilinmeyenler

| Konu | Sonuç |
|------|-------|
| Banka kimliği tipi | 4-hane `BankCode` (Guid değil), yerel katalog |
| Currency alanı | YOK (yalnız TL) |
| SWIFT / şube | YOK (yurtiçi TL, IBAN yeterli) |
| Mesaj kodları | Common'daki mevcut sabitler (yeni dosya yok) |
| Schema | `MerchantSchemaName` (mevcut Merchant BC şeması) |
| Yetki | Ertelendi (endpoint korumasız), tenant filtre var |