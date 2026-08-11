# Implementation Plan: Komisyon Teklifi ve Metin-Sürümlü Pazarlık

**Branch**: `019-commission-proposal-acceptance` | **Date**: 2026-08-11 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/019-commission-proposal-acceptance/spec.md`

## Summary

Her yeni merchant'a standart komisyon teklifi (banka grid'i + config marjı) Excel ekli + kabul/ret
linkli mail ile sunulur. Admin tüm süreci Merchant.Agent üzerinden METİNLE yürütür (teklif sun,
taslak revize "satır 37'yi 1.85 yap", taslağı göster, durum sor); Commission.Api'ye ilk kez `/mcp`
yüzeyi açılır. Kabul tamamen insansızdır: taslak `MerchantCommission`'a kopyalanır,
`MerchantCommissionGridReady` mevcut aktivasyon zincirini tetikler. Ret gerekçesiyle döner; admin
revize eder, açık "gönder" komutuyla yeni tur başlar. Excel üretimi Mail.Worker'da (generic tablo
eki), karar uçları Commission.Api'de anonim-biletli mini HTML. `FinalizeMerchantCommissionGrid` +
Admin UI Finalize butonu kalkar.

## Technical Context

**Language/Version**: C# / .NET 10 (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Marten (Postgres), Wolverine (in-proc bus + RabbitMQ fanout),
ModelContextProtocol (MCP server/client), Microsoft Agent Framework (A2A, preview pin),
ClosedXML (yeni: Mail.Worker'a eklenir), OpenIddict token (mevcut Identity.Server)

**Storage**: Commission BC kendi Marten DB'si (`CommissionDraft` + `CommissionProposal` yeni
dökümanlar; `MerchantCommission`/`BankCommission` mevcut). BC sınırı sert (Anayasa I).

**Testing**: xUnit saf domain birim testleri (`tests/Commission.Api.Tests` genişler);
handler/HTTP/agent akışı quickstart ile elle doğrulanır (house-style).

**Target Platform**: .NET Aspire orkestrasyonu (AppHost); Mailpit dev SMTP.

**Project Type**: Mikroservis (mevcut çözüm içinde dikey genişleme; yeni proje YOK).

**Performance Goals**: SC-001 — teklif komutundan mail kuyruğuna ≤5 sn (tek outbox publish).

**Constraints**: LLM oran üretmez/hesaplamaz (tool'lar yalnız açık değer taşır; toplu op matematiği
sunucuda). Karar uçları anonim, yetki = tek-kullanımlık + TTL bilet. Kabul sonrası değişmezlik.

**Scale/Scope**: Merchant başına tek aktif teklif; taslak ~yüzlerce satır (banka×taksit).

## Constitution Check

*GATE: v1.4.0'a göre değerlendirildi — GEÇTİ (bir amendment notu var).*

- **I. BC İzolasyonu**: ✅ Yeni aggregate'ler Commission BC içinde; Merchant BC'ye geçiş yalnız
  mevcut `MerchantCommissionGridReady` integration event'iyle. Mail, `SendEmailRequested`
  (Shared kontrat) genişletilerek. DB paylaşımı yok.
- **II. Zengin Domain**: ✅ `CommissionDraft`/`CommissionProposal` davranışlı aggregate; bilet/
  taban/kilit invariant'ları aggregate metotlarında. ⚠️ Anayasa metni hâlâ "BaseModel'den türer"
  ve "Enumeration ile modellenir" diyor — ikisi de bugün silindi (AggregateRoot'a çöktü, düz enum
  kullanılıyor). Koddaki fiili durumla çelişki bu feature'dan bağımsız; **anayasa PATCH amendment
  gerekli** (ayrı iş, bkz. research.md R7).
- **III. Vertical Slice + CQRS**: ✅ `Domains/CommissionDrafts|CommissionProposals/Features/
  {Commands,Queries,Agents}`; agent slice'ları kendi kendine yeter (CLAUDE.md 015 kuralları).
- **IV. Result Pattern**: ✅ `ResultDomain`/`FeatureObjectResultModel<T>`; taban ihlali/geçersiz
  satır/bilet hataları MessageItem ile.
- **V. Merkezi Kimlik**: ✅ `/mcp` yüzeyi `commission.write` policy; Merchant.Agent kendi client'ı
  ile token alır (scope genişlemesi gerekir — research R5). Karar uçları bilinçli anonim
  (FR-004, aktivasyon redeem emsali); bilet = yetki, "varsayılan açık uç" değil.
- **VI. Spec-Driven**: ✅ Tam akış (spec → plan → tasks → implement).

## Project Structure

### Documentation (this feature)

```text
specs/019-commission-proposal-acceptance/
├── plan.md              # Bu dosya
├── research.md          # Faz 0 kararları
├── data-model.md        # Faz 1 — aggregate/durum modeli
├── quickstart.md        # Faz 1 — canlı doğrulama senaryoları
├── contracts/           # Faz 1 — MCP tool + HTTP + mesaj kontratları
└── tasks.md             # /speckit-tasks üretir
```

### Source Code (repository root)

```text
src/services/Commission.Api/
├── Domains/CommissionDrafts/
│   ├── CommissionDraft.cs                      # YENİ aggregate (çalışma kopyası, satır no'lu)
│   └── Features/Agents/
│       ├── ReviseCommissionDraftForAgent.cs    # set/delta/satır-no revizyon + diff yanıtı
│       └── ShowCommissionDraftForAgent.cs      # satır no'lu tam tablo
├── Domains/CommissionProposals/
│   ├── CommissionProposal.cs                   # YENİ aggregate (gönderilmiş fotoğraf + bilet)
│   ├── CommissionProposalMcpTools.cs           # MCP tool'ları (aggregate kökü, 015 kuralı)
│   ├── CommissionProposalEndpointExtension.cs  # karar uçları + mini HTML
│   └── Features/
│       ├── Agents/
│       │   ├── SubmitCommissionProposalForAgent.cs   # teklif sun / yeniden gönder
│       │   └── CommissionProposalStatusForAgent.cs   # durum + gerekçe
│       └── Commands/
│           ├── AcceptCommissionProposal.cs     # anonim biletli kabul (mini HTML + POST)
│           └── RejectCommissionProposal.cs     # anonim biletli ret + gerekçe formu
├── Domains/MerchantCommissions/
│   ├── Features/Commands/FinalizeMerchantCommissionGrid.cs   # SİLİNİR (FR-013)
│   └── MerchantCommissionGrid.cs               # GridStatus/Draft-Ready kalkar (FR-013)
├── Options/CommissionProposalOption.cs         # YENİ: marj + bilet TTL + link taban adresi
└── Program.cs                                  # AddMcpServer + MapMcp("/mcp") eklenir

src/others/Shared/IntegrationEvents.cs          # SendEmailRequested'e EmailAttachmentTable eklenir
src/others/Mail.Worker/
├── SendEmailHandler.cs                         # tablo → xlsx (ClosedXML) → SMTP eki
└── Mail.Worker.csproj                          # + ClosedXML (CPM)

src/agents/Merchant.Agent/
├── McpToolProvider.cs                          # + Commission.Api /mcp client
├── MerchantAgentCard.cs                        # + komisyon skill'leri
└── Options/                                    # + Commission MCP adresi

src/ui/Admin/                                   # Finalize butonu kalkar; teklif durumu salt-okuma
tests/Commission.Api.Tests/                     # draft/proposal domain testleri
```

**Structure Decision**: Yeni proje açılmaz. Commission BC iki yeni aggregate klasörü alır
(aggregate-klasör kuralı: klasör başına tek `AggregateRoot`). Merchant.Agent mevcut A2A host'una
ikinci MCP client + yeni skill'ler eklenir. Excel üretimi Mail.Worker'da (generic tablo eki).

## Complexity Tracking

İhlal yok. Anayasa II'deki bayat BaseModel/Enumeration atfı bu feature'ın ihlali değil,
bugünkü refactor'ün (BaseModel çöküşü) ertelenmiş amendment'ı — research R7'de kayıtlı.