# Phase 0 Research: Payment.Api iyzico Wire Material Geçişi

025/026/027 ile birebir desen. Zemin [[decisions_iyzico_sdk_ddd_adaptation]].

## R1 — Üç klasör nereye taşınır

- **Decision**: `Domains/{Payments,Installments,StoredCards}` → `Provider/{Payments,Installments,
  StoredCards}`, namespace `Payment.Api.Provider.{X}`. Origin klasör adları korunur (çoğul).
- **Rationale**: iyzico ödeme/taksit/kart wire/istemci malzemesi. CP.VPOS-sınırı: sağlayıcı tipleri
  `Domains/` geçmez. Çoğul klasör adı `Payment` tipiyle segment çakışması yaratmaz (segment `Payments`
  ≠ tip `Payment`).
- **Alternatives**: Tekil klasör adları (`Payment` → tip çakışması); tek birleşik klasör (üç ayrı
  iyzico ürün alanı — ayrık dursun).

## R2 — Payment.Api gerçek domain'i yok → Domains boşalır

- **Decision**: Üç klasör taşınınca `Payment.Api/Domains/` TAMAMEN boşalır; üç alt-klasör silinir.
  Boş `Domains/` parent'ı kalabilir (git boş dizin izlemez; gerçek Payment domain'i sonra oraya gelir).
- **Rationale**: 022 pivotu Payment aggregate/endpoint/slice'ı sildi; `Domains/` %100 wire. Wire
  çıkınca domain kalmaz — beklenen ara durum. Charge akışı domain'i davranış spec'inde kurulur.
- **Alternatives**: Placeholder aggregate koymak (YAGNI — davranış yok).

## R3 — Klasörler-arası + resource+çağrı deseni

- **Decision**: Cross-folder referanslar (ör. `PaymentItem`→`ConvertedPayout`, `Payment`→`Buyer`)
  korunur: üç yeni Provider namespace'i GlobalUsings'e eklenir → global görünür. Resource+static-çağrı
  birleşik SDK deseni + enum'lar + DTO'lar aynen taşınır, bölünmez.
- **Rationale**: Tümü GlobalUsings ile global çözülüyor; namespace değişse de global using korur. SDK
  deseni 025/026/027 ile tutarlı. Bölmek gold-plating.
- **Alternatives**: Explicit using ekleme (gereksiz — global using deseni mevcut); DTO/çağrı ayrımı (YAGNI).

## R4 — Referans/derleme güvenliği

- **Decision**: Taşıma güvenli — dış referans yalnız 3 `GlobalUsings.cs` satırı (`Domains.Payments/
  .Installments/.StoredCards`). Program.cs/Agent/Admin/test bu tipleri kullanmaz. Üç satır
  `Provider.{X}`'e güncellenir.
- **Rationale**: `grep` (folder dışı): yalnız GlobalUsings; ui/agents'ta yok; Payment test projesi
  yok. Namespace değişimi derlemeyi kırmaz.
- **Alternatives**: Yok — olgusal.

## R5 — Taşıma yöntemi

- **Decision**: Klasör başına `git mv Domains/X/*.cs Provider/X/` + taşınan dosyalarda namespace
  `Payment.Api.Domains.X` → `Payment.Api.Provider.X`. Base tipler (`Payment.Api.Provider`) child
  namespace'ten görünür.
- **Rationale**: 026/027 ile aynı; her klasör kendi namespace'inde birlikte taşınır → intra-referans
  çözülür; cross-folder GlobalUsings ile.
- **Alternatives**: Elle yeniden yaz (40 dosya — hataya açık).

## Çözülmemiş NEEDS CLARIFICATION

Yok. Spec 0 marker; R1–R5 sabitledi.
