# Implementation Plan: A2A Ödeme Oturumu — Kayıtlı Kartla Taksitli Ödeme (Model A)

**Branch**: `007-a2a-payment-session` | **Date**: 2026-08-02 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/007-a2a-payment-session/spec.md`

## Summary

E-ticaret asistanının doğal dille verdiği ödeme niyetini **A2A** üzerinden kabul eden, kart
verisini kanala sokmadan (yalnız token) **taksit seçeneklerini getiren** ve **kullanıcının
taksit seçimini oturuma yazan** bir yüzey. Fiyatlama **Model A**: kullanıcı sepet tutarını
görür; banka komisyonu yalnız en ucuz POS'u seçmek için `BankRouter`'a girer, kullanıcı
tutarına eklenmez.

**Kapsam (KARARLAŞTIRILDI):** 007 = A2A + Payment agent (LLM) + MCP + Faz 1 quote + taksit
seçiminin oturuma yazılması + durum sorgu. **Fiili ödeme çekimi (pay) 007 DIŞI** — `ProcessPayment`
yeniden kurgulanacak ayrı/daha büyük bir feature; 007 seçilen taksiti bir *seam*'e devreder.

**Teknik yaklaşım:**

- **Yeni proje `src/agents/Payment.Agent`** (ASP.NET, Aspire node): A2A server + LLM `AIAgent`
  (Microsoft Agent Framework) + MCP client. Payment BC **değil** — kalıcılık yok, stateless
  yönlendirici. LLM yalnız tool sırasını kurar (quote → select); para/banka/kart üretmez.
- **Payment.Api** (mevcut Payment BC) kazanır:
  1. **`PaymentSession` aggregate** (yeni `Domains/PaymentSessions/`) — A2A task'ının kalıcı
     domain izdüşümü; faz makinesi (açıldı → quote verildi → taksit seçildi / başarısız).
  2. **MCP server** (`ModelContextProtocol.AspNetCore`, `WithToolsFromAssembly()` + `MapMcp("/mcp")`)
     — 3 tool: `get_installment_options`, `select_installment`, `payment_status`. Tool'lar
     `Domains/PaymentSessions/PaymentSessionMcpTools.cs` içinde ince `[McpServerToolType]` sarmalayıcı;
     yalnız `Features/Agent/` slice'ını `IMessageBus.InvokeAsync` ile çağırır (ECommerce deseni).
     Tutar **session'dan** okunur.
- **Model A quote**: yeni `QuoteInstallmentsForSession` feature'ı kullanıcı tutarını = sepet
  tutarı üretir; komisyonu **eklemez** (mevcut Model B `GetInstallmentOptions` bu akışta
  kullanılmaz — bkz. research.md R6).
- **Token → kart bilgisi**: `ICardVault` seam'i. 007'de yalnız **BIN/kart-programı** gerekir
  (tam PAN çekim feature'ında). BIN → `CardInfo` çözümü **008 (BinCard→DB, merge edildi)** ile
  gelen `ResolveBinCard`'tan alınır — **CP.VPOS BinService kullanılmaz**. `SimulatedCardVault`
  yalnız **token → BIN** eşlemesini simüle eder (gerçek tokenizasyon/PAN saklama = ayrı/sonraki
  feature); BIN'i `ResolveBinCard.Resolve(session, bin)` çözer. Tümü server-side, A2A/LLM
  kanalını **geçmez**.
- **A2A + MCP + Agent Framework** resmi köprü paketleriyle birleşir (elle protokol yazılmaz).

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable` + `ImplicitUsings` açık — anayasa)

**Primary Dependencies**:
- Mevcut (Payment.Api): Marten 9.5, WolverineFx 6.4, CP.VPOS, Asp.Versioning
- Yeni (Payment.Api): `ModelContextProtocol.AspNetCore` (GA 2.0.0) — MCP server
- Yeni (Payment.Agent): `Microsoft.Agents.AI` + `.OpenAI` + `.Hosting.A2A` + `.Hosting.A2A.AspNetCore`
  (preview ~1.16), `A2A` + `A2A.AspNetCore` (preview2), `ModelContextProtocol.Core` (MCP client),
  `Microsoft.Extensions.AI`
- Aspire chat resource: `Aspire.Hosting.GitHub.Models` (dev) / `Aspire.Hosting.Azure.CognitiveServices` (prod)

**Storage**: Marten (Postgres) — `PaymentSession` `paymentDb`'de. Payment.Agent kalıcılık tutmaz.

**Testing**: Saf domain birim testleri (`tests/Payment.Api.Tests` — yeni): `PaymentSession` faz
geçişleri, seçim ⊂ sunulanlar, Model A tutar (sapma 0), quote filtreleme, boş-liste → başarısız.
A2A/MCP/LLM birim testi **yok** — quickstart ile elle (anayasa test kuralı).

**Target Platform**: Linux/container, Aspire orchestrated (AppHost)

**Project Type**: Mikroservis (Payment BC genişletme) + yeni protokol-adaptör host (Payment.Agent)

**Performance Goals**: Etkileşimli agent akışı; sıkı latency yok. LLM 2 tool (quote, select). N/A ölçek.

**Constraints**: Yalnız TL. Kart verisi (PAN/CVV) A2A/LLM kanalını **geçmez** (007'de PAN hiç
gerekmez — yalnız BIN). Yetki yok (proje-geneli erteleme).

**Scale/Scope**: Yeni 1 proje (Payment.Agent); Payment.Api'ye 1 aggregate + 3 feature slice +
MCP tool sınıfı + `ICardVault` seam. AppHost'a 1 proje + 1 chat resource.

## Constitution Check

*GATE: Phase 0 öncesi geçti. Phase 1 sonrası tekrar bakıldı — ihlal yok.*

| İlke | Durum | Not |
|------|-------|-----|
| I. Bounded Context İzolasyonu | ✅ | `PaymentSession` Payment BC içinde; yeni BC/DB yok. `Payment.Agent` BC **değil** (kalıcılık yok, delivery adaptörü). Agent→Api = MCP-over-HTTP, iç iletişim (aynı org). Cross-context DB erişimi yok. |
| II. Zengin Domain Modeli | ✅ | `PaymentSession` = private setter + statik `Create` + faz metotları (`OfferInstallments`, `SelectInstallment`, `Fail`); invariant'lar aggregate'te (seçim ⊂ sunulanlar, faz sırası, Model A satır kuralı). Koleksiyon private + readonly. |
| III. Vertical Slice + CQRS | ✅ | 3 slice `Features/Agent/` altında (ECommerce deseni — agent'a açık işlemler `Agent` klasöründe): `QuoteInstallmentsForSession`, `SelectInstallment`, `GetPaymentSessionStatus`. Static class (record+Response+Handler). MCP tool'ları ince sarmalayıcı (`PaymentSessionMcpTools`), yalnız slice'ı `IMessageBus.InvokeAsync` ile çağırır. Repository yok. |
| IV. Result Pattern | ✅ | Handler'lar `FeatureObjectResultModel<T>`/`ResultDomain` döner; hata `MessageItem` + resource sabiti. MCP tool sınırında Result → tool cevabı. |
| V. Merkezi Kimlik & Açık Yetki | ⚠️ ertelenmiş | Proje-geneli AUTHZ ertelemesi. A2A yüzeyi korumasız; güvenlik sınırı "kart verisi kanala girmez" (007'de PAN hiç yok). Session-isolation notu research'te. |
| VI. Spec-Driven | ✅ | spec→plan→tasks→implement. |

**Teknoloji kısıtları:** .NET 10 + Aspire ✅ · Marten ✅ · Wolverine ✅ · CPM (yeni paketler
`Directory.Packages.props`'a) ✅ · yalnız TL ✅ · CP.VPOS tipleri slice'ı geçmez (handler çevirir) ✅.

## Project Structure

### Documentation (this feature)

```text
specs/007-a2a-payment-session/
├── spec.md
├── plan.md              # bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/           # Phase 1 (agent-card + MCP tools + A2A akışı)
└── tasks.md             # /speckit-tasks (bu komut üretmez)
```

### Source Code (repository root)

```text
src/
├── agents/                                  # YENİ klasör
│   └── Payment.Agent/                        # YENİ proje — A2A host + LLM router + MCP client
│       ├── Program.cs                        # AddA2AServer(agent) + MapA2AHttpJson/JsonRpc + MapWellKnownAgentCard
│       ├── PaymentAgentFactory.cs            # IChatClient + MCP tools → AIAgent (instructions = router)
│       ├── PaymentAgentCard.cs               # AgentCard: skills quote-installments / select-installment / payment-status
│       ├── McpToolProvider.cs               # ECommerce deseni: McpClient.CreateAsync(HttpClientTransport → http://payment-api/mcp), ListToolsAsync → AITool[] (auth yok → PerUserMcpTool gerekmez)
│       ├── GlobalUsings.cs
│       └── Payment.Agent.csproj
│
├── services/Payment.Api/                    # mevcut Payment BC — genişletilir
│   ├── Domains/PaymentSessions/             # YENİ domain
│   │   ├── PaymentSession.cs                 # aggregate + PaymentSessionStatus enum + OfferedInstallment VO
│   │   ├── Features/Agent/                   # AGENT'A AÇIK slice'lar (ECommerce deseni: Features/Agent)
│   │   │   ├── QuoteInstallmentsForSession.cs   # session aç + Model A taksit listesi
│   │   │   ├── SelectInstallment.cs             # seçim doğrula (⊂ sunulanlar) + session'a yaz
│   │   │   └── GetPaymentSessionStatus.cs
│   │   └── PaymentSessionMcpTools.cs         # [McpServerToolType]×3 (get_installment_options / select_installment /
│   │   │                                     #   payment_status) — her tool ilgili Features/Agent slice'ını bus.InvokeAsync ile sarar
│   ├── CardVault/
│   │   ├── ICardVault.cs                     # token → CardInfo (BIN/banka/program) — server-side, PAN yok
│   │   └── SimulatedCardVault.cs             # token→BIN simüle; BIN→CardInfo = 008 ResolveBinCard (CP.VPOS DEĞİL)
│   └── Program.cs                            # + AddMcpServer().WithToolsFromAssembly() + MapMcp("/mcp")
│
└── aspire/AppHost/AppHost.cs                # + payment-agent projesi + chat model resource + reference'lar

tests/
└── Payment.Api.Tests/                       # YENİ — saf domain birim testleri (PaymentSession)
```

**Structure Decision**: Mevcut mikroservis + Aspire düzeni korunur. Yeni `src/agents/` altında
`Payment.Agent` (delivery adaptörü, BC değil). Domain ağırlığı (session + Model A quote + tool
sarma) Payment.Api içinde — anayasa BC izolasyonu. Payment.Agent ince + stateless.

## Seam: pay devir noktası (007 → sonraki pay feature)

007 seçilen taksiti fiili çekime **bağlamaz**. Bağlantı noktası:

- `SelectInstallment` handler'ı seçimi `PaymentSession`'a yazar; oturum `InstallmentSelected`
  fazında **durur**. Çekim tetiklenmez, `Payment` kaydı oluşmaz.
- Sonraki pay feature'ı bu oturumu (token + sepet tutarı + seçilen taksit) okuyup yeniden
  kurgulanacak `ProcessPayment` hattını çalıştıracak. 007 hiçbir `ISalePipeline` **implemente
  etmez** — yalnız oturumu tüketilebilir durumda bırakır (YAGNI: kullanılmayan soyut seam yazma).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Yeni proje `Payment.Agent` | A2A + Agent Framework + LLM bağımlılıkları hepsi **preview**, haftalık breaking churn; saf Payment BC servisini kirletmemek. "Sınırda opak agent" mimarisini (spec §Mimari) fiziksel izole etmek. Kullanıcı kararı. | Tek-servis (Payment.Api içinde in-proc agent): preview LLM/A2A paketlerini kararlı ödeme servisine sokar; dağıtım/güvenlik yüzeyini karıştırır. |
| MCP-over-HTTP (Agent→Api) | İki ayrı process; MCP spec'in "içeride MCP" sözleşmesi. `McpClientTool : AIFunction` olduğundan Agent Framework'e adaptörsüz girer. | Düz HTTP/REST client: spec MCP diyor; MCP tool şeması LLM function-calling'e doğal, REST elle şema gerektirir. |