# Implementation Plan: Reference.Api Removal

**Branch**: `021-reference-api-removal` | **Date**: 2026-08-13 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/021-reference-api-removal/spec.md`

## Summary

Reference.Api (event-only referans-veri BC'si) sistemden tamamen sökülür: proje + testler +
`referenceDb` + AppHost kaydı + `ReferenceDataUpdated` sözleşmesi + iki BC'deki yerel
read-model kopyaları ve tüketici handler'ları silinir. Kataloğa bağlı davranışlar bilinçli
sadeleşir: Commission banka tanımı kod+ad kullanıcı girdisiyle çalışır, `GetBankCatalog` ve
Admin dropdown'ları kalkar (serbest giriş), settlement banka doğrulaması/ad zenginleştirmesi
kalkar (IBAN mod-97 korunur), merchant sorguları ad zenginleştirmesiz döner. SharedKernel
CardTaxonomy korunur. iyzico pivotunun (022-024) ilk temizlik adımı.

## Technical Context

**Language/Version**: C# / .NET 10 (mevcut çözüm standardı; yeni proje eklenmez, iki proje silinir)

**Primary Dependencies**: Marten (read-model şema kayıtları sökülür), Wolverine + RabbitMQ
(fanout exchange `reference.data-updated` declare/bind sökülür), Aspire (servis + DB kaydı sökülür)

**Storage**: `referenceDb` tanımı kalkar; Merchant/Commission DB'lerindeki read-model
dokümanları dev sıfırlamasıyla gider (migration yok — proje pratiği)

**Testing**: `tests/Reference.Api.Tests` (21 test) silinir; kalan 3 BC test projesi + Iyzipay.Tests
yeşil kalmalı. Etkilenen slice'ların davranış değişimi quickstart ile elle doğrulanır (proje
konvansiyonu: handler/HTTP testi yazılmaz)

**Target Platform**: Mevcut Aspire orkestrasyonu (Postgres + RabbitMQ)

**Project Type**: Söküm/refactor — mevcut mikroservis çözümünde proje kaldırma + bağımlı slice sadeleştirme

**Performance Goals**: N/A (kaldırma işi; sıcak yol değişmez, sorgu başına 1-2 katalog Load'u eksilir)

**Constraints**: Kalan akışlar çalışamaz duruma düşmez (FR-005/006); katalogdan bağımsız
kurallar (IBAN mod-97, kod benzersizliği, merchant-varlık) aynen korunur; SharedKernel
CardTaxonomy dokunulmaz (FR-007); veri migration üretilmez (dev aşaması)

**Scale/Scope**: 2 proje silinir; ~12 dosya düzenlenir (2 Program.cs, 2 Shared dosyası,
AppHost, slnx, 5 slice, 1 agent slice, Admin 3 sayfa + client); 2 read-model dosyası silinir

## Constitution Check

*GATE: Anayasa v1.4.0'a göre değerlendirildi (Phase 0 öncesi + Phase 1 sonrası yeniden).*

| İlke | Değerlendirme | Sonuç |
|------|---------------|-------|
| I. BC İzolasyonu | Söküm izolasyonu GÜÇLENDİRİR: cross-BC UI-composition (Admin settlement formunun Commission'dan katalog çekmesi) ortadan kalkar; BC'ler arası kalan tek bağ mevcut event sözleşmeleri | PASS |
| II. Zengin Domain | Aggregate'lere dokunuş minimal: `Bank.Create` imzası aynı (ad kaynağı komuta taşınır); `SettlementAccount.Create` değişmez; invariant kaybı yok — kaldırılan kontroller katalog-varlık kontrolleriydi (handler'daydı, aggregate'te değil) | PASS |
| III. Vertical Slice + CQRS | Değişiklikler slice-içi kalır; `GetBankCatalog` slice'ı bütün olarak silinir (slice = silme birimi) | PASS |
| IV. Result Pattern | Mevcut Result akışları korunur; silinen yalnız NotFound dalları | PASS |
| V. Merkezi Kimlik/Yetki | Endpoint policy'leri değişmez; silinen `GetBankCatalog` zaten `commission.read` policy'liydi — uç tamamen kalkıyor, açıkta uç kalmıyor. Identity'de reference scope/istemci yok (R1) | PASS |
| VI. Spec-Driven | Tam akış izleniyor | PASS |
| Teknoloji: Aspire/Marten/Wolverine | Kayıtlar konvansiyona uygun sökülür; yeni altyapı eklenmez | PASS |
| Test konvansiyonu | Saf domain testleri korunur; silinen yalnız Reference'ın kendi testleri | PASS |

**Post-Phase-1 yeniden değerlendirme**: Tasarım yeni ihlal getirmedi; Complexity Tracking boş. GEÇTİ.

## Project Structure

### Documentation (this feature)

```text
specs/021-reference-api-removal/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 çıktısı (R1-R8: bağ haritası + kararlar)
├── data-model.md        # Phase 1 çıktısı (silinen/değişen varlıklar)
├── contracts/
│   └── api-changes.md   # Phase 1 çıktısı (kalkan uç + değişen istek/yanıt şekilleri)
├── quickstart.md        # Phase 1 çıktısı (doğrulama senaryoları)
└── tasks.md             # Phase 2 çıktısı (/speckit-tasks — bu komut üretmez)
```

### Source Code (repository root)

```text
# SİLİNEN:
src/services/Reference.Api/            # proje kökten (Domains, Seeding, Program)
tests/Reference.Api.Tests/             # 21 test
src/services/Merchant.Api/ReadModels/ReferenceReadModels.cs
src/services/Commission.Api/ReadModels/ReferenceBankReadModel.cs
src/services/Commission.Api/Domains/Banks/Features/Queries/GetBankCatalog.cs   # slice bütün

# DÜZENLENEN:
PaymentGateway.slnx                    # 2 proje girdisi çıkar
src/aspire/AppHost/AppHost.cs          # referenceDb + reference-api bloğu
src/others/Shared/IntegrationEvents.cs # ReferenceDataUpdated + ReferenceItem çıkar
src/others/Shared/RabbitMqConstants.cs # ReferenceDataUpdated sınıfı çıkar
src/services/Merchant.Api/Program.cs   # 4 şema kaydı + exchange/queue bloğu
src/services/Commission.Api/Program.cs # 1 şema kaydı + exchange/queue bloğu
src/services/Merchant.Api/Domains/SettlementAccounts/Features/Commands/{Create,Update}SettlementAccount.cs
src/services/Merchant.Api/Domains/SettlementAccounts/Features/Queries/{GetSettlementAccounts,GetSettlementAccount}.cs
src/services/Merchant.Api/Domains/Merchants/Features/Queries/{GetMerchant,GetMerchantByKey}.cs
src/services/Merchant.Api/Domains/Merchants/Features/Agents/GetMerchantForAgent.cs
src/services/Commission.Api/Domains/Banks/Features/Commands/CreateBank.cs      # Name komuttan
src/services/Commission.Api/Domains/Banks/BankEndpointExtension.cs             # (katalog ucu kaydı varsa)
src/services/Commission.Api/Domains/CommissionProposals/Features/Agents/SubmitCommissionProposalForAgent.cs
src/ui/Admin/Clients/CommissionApiClient.cs   # GetBankCatalogAsync + BankCatalogItem çıkar
src/ui/Admin/Clients/ApiModels.cs             # katalog modelleri + yanıt modellerinden ad alanları
src/ui/Admin/Pages/Banks/Create.cshtml(.cs)   # dropdown → Code+Name text input
src/ui/Admin/Pages/SettlementAccounts/Create.cshtml(.cs) + Edit.cshtml(.cs)    # dropdown → BankCode input
CLAUDE.md                              # Reference izleri (010 bağlamı) gözden geçirilir
```

**Structure Decision**: Söküm mevcut vertical-slice yapısını izler: slice bütün olarak
silinir (`GetBankCatalog`) veya slice-içi düzenlenir; BC sınırları dışına taşan tek şey
paylaşılan sözleşme temizliğidir (`Shared`). Yeni proje/klasör açılmaz.

## Complexity Tracking

> Boş — Constitution Check ihlal içermiyor (söküm anayasal yapıyı sadeleştiriyor).