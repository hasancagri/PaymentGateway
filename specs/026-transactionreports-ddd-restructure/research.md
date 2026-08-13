# Phase 0 Research: TransactionReports Yapısal DDD Geçişi

025 SubMerchants ile birebir desen; kararlar yapısal. Zemin [[decisions_iyzico_sdk_ddd_adaptation]].

## R1 — Wire/istemci tipleri nereye taşınır

- **Decision**: 13 tip → `Commission.Api/Provider/Reporting/`, namespace `Commission.Api.Provider.Reporting`.
  `Domains/TransactionReports/` silinir.
- **Rationale**: iyzico rapor API wire/istemci malzemesi (`BaseRequestV2`/`ProviderResourceV2` türevi,
  PKI, canlı HTTP `/v2/reporting/payment/transactions`). CP.VPOS-sınırı: sağlayıcı tipleri `Domains/`
  geçmez. Klasör adı `Reporting` (iyzico endpoint grubu; tip adıyla çakışmaz).
- **Alternatives**: `Domains/`'de tutmak (ihlal); ayrı proje (BC-içi, aşırı).

## R2 — Nested DTO'lar + resource+çağrı birleşik deseni

- **Decision**: 6 nested DTO (`TransactionReportItem`, `TransactionDetailItem`,
  `TransactionDetailCancelItem`, `PaymentTxDetailItem`, `RefundDetailItem`, `ConvertedPayout`) ve
  resource'lar (DTO+static HTTP çağrısı birleşik) AYNEN sağlayıcı tarafına taşınır; bölünmez.
- **Rationale**: SDK'nın idiomatik deseni; Payment.Api/diğer Provider malzemesiyle tutarlı. Bölmek
  gold-plating (025 R5). Nested DTO'lar resource yanıtının parçası — birlikte durmalı.
- **Alternatives**: DTO/çağrı ayrımı (tutarsız, YAGNI).

## R3 — `Domains/TransactionReports/` klasörünün kaderi

- **Decision**: Klasör tamamen DAĞITILIR (silinir); geride domain aggregate/tip kalmaz.
- **Rationale**: Aggregate-klasör kuralı: `Domains/<X>/` bir `: AggregateRoot` içerir. Bu klasörde
  aggregate YOK (yalnız wire tipleri). Wire çıkınca aykırı klasör kalır — dağıtmak kuralı geri getirir.
- **Alternatives**: Boş klasör bırakmak (kural ihlali).

## R4 — 024 CommissionPolicy'ye etki

- **Decision**: 024 DOKUNULMAZ. `CommissionPolicy` bu tipleri KULLANMAZ (maliyeti string girdi alır);
  yalnız `CommissionPolicy.cs`'te bir doc-yorumda `TransactionReportItem` adı geçer (tip referansı
  DEĞİL). Yorum güncellenmez (domain diff'i sıfır tutmak için gerekmez).
- **Rationale**: FR-005/SC-005: 024 domain + yüzey değişmez; `Domains/CommissionPolicies/` diff = 0.
  Doc-yorumdaki isim taşımayı etkilemez (derleme string yorum çözmez).
- **Alternatives**: Yorumu güncellemek (domain dosyasına dokunur — guardrail'i gevşetir, gereksiz).

## R5 — Referans/derleme güvenliği

- **Decision**: Taşıma güvenli — TransactionReport tipleri hiçbir yerde KULLANILMAZ. Dış geçişler:
  `GlobalUsings.cs` satırı (`global using Commission.Api.Domains.TransactionReports;`) + bir doc-yorum.
  Payouts çapraz-ref yok. GlobalUsings satırı `Commission.Api.Provider.Reporting`'e güncellenir.
- **Rationale**: `grep -rn` (folder dışı): yalnız GlobalUsings + CommissionPolicy.cs doc-yorum.
  ui/others yok. Testler bu tipleri kullanmaz. Namespace değişimi derlemeyi kırmaz.
- **Alternatives**: Yok — olgusal.

## R6 — Taşıma yöntemi

- **Decision**: `git mv` ile dosyaları taşı (git geçmişi korunur), sonra taşınan dosyalarda namespace
  satırını `Commission.Api.Domains.TransactionReports` → `Commission.Api.Provider.Reporting` değiştir.
  Base tipler (`ProviderResourceV2`/`BaseRequestV2`, namespace `Commission.Api.Provider`) yeni child
  namespace'ten görünür (using gerekmez).
- **Rationale**: Tümü aynı namespace'te → intra-referanslar niteliksiz; hepsi birlikte yeni namespace'e
  taşınınca çözülür. `Commission.Api.Provider.Reporting`, `Commission.Api.Provider`'ın alt'ı → parent
  tipleri görür.
- **Alternatives**: Dosya-dosya elle yeniden yaz (26 işlem, hataya açık).

## Çözülmemiş NEEDS CLARIFICATION

Yok. Spec 0 marker; R1–R6 sabitledi.
