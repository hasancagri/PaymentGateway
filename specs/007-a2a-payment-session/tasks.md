---
description: "Task list — 007 A2A Ödeme Oturumu (taksit seçimine kadar, Model A)"
---

# Tasks: A2A Ödeme Oturumu — Kayıtlı Kartla Taksitli Ödeme (Model A)

**Input**: Design documents from `/specs/007-a2a-payment-session/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Yalnız saf domain birim testleri (anayasa). A2A/MCP/LLM entegrasyonu birim testi
edilmez — quickstart.md ile elle doğrulanır. Test görevleri sadece `PaymentSession` + Model A
saf hesap içindir.

**Organization**: Görevler user story'ye göre gruplu (US1 quote / US2 select / US3 status).

## Desen kararı (ECommerce'den, kullanıcı onayı)

- **Agent'a açık işlemler** `Domains/PaymentSessions/Features/Agent/` altında slice (record +
  Response + Handler; endpoint yok — MCP ile erişilir).
- **MCP tool'ları** `Domains/PaymentSessions/PaymentSessionMcpTools.cs` içinde, her tool ayrı
  `[McpServerToolType]` static class; gövde yalnız `IMessageBus.InvokeAsync` ile ilgili
  `Features/Agent` slice'ını çağırır. Kayıt: `AddMcpServer().WithToolsFromAssembly()` + `MapMcp("/mcp")`.
- **BIN çözümü** 008'in `ResolveBinCard.Resolve(session, bin)` → `CardInfo?` (CP.VPOS BinService DEĞİL).

## Path Conventions

Mikroservis + Aspire. Payment BC: `src/services/Payment.Api/`. Yeni agent host:
`src/agents/Payment.Agent/`. Testler: `tests/Payment.Api.Tests/`. Orkestrasyon:
`src/aspire/AppHost/AppHost.cs`. Paketler: `Directory.Packages.props` (CPM).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Proje iskeleti + paket sürümleri.

- [X] T001 Yeni proje `src/agents/Payment.Agent/Payment.Agent.csproj` oluştur (ASP.NET Core Web,
  `net10.0`, `Nullable`+`ImplicitUsings` açık); `PaymentGateway.slnx`'e ekle. `ServiceDefaults`
  ve `Payment.Api` MCP endpoint'i için Aspire referansları T011'de bağlanır.
- [X] T002 [P] Yeni test projesi `tests/Payment.Api.Tests/Payment.Api.Tests.csproj` oluştur
  (xUnit, `net10.0`, `Payment.Api`'ye ProjectReference); `PaymentGateway.slnx`'e ekle.
- [X] T003 [P] `Directory.Packages.props`'a yeni paket sürümlerini ekle (hepsi pin —
  research R1-R5): `ModelContextProtocol.AspNetCore`, `ModelContextProtocol.Core` (2.0.0),
  `Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`, `Microsoft.Agents.AI.Hosting.A2A`,
  `Microsoft.Agents.AI.Hosting.A2A.AspNetCore` (~1.16), `A2A`, `A2A.AspNetCore` (1.0.0-preview2),
  `Microsoft.Extensions.AI`, `Aspire.Hosting.GitHub.Models`.
- [X] T004 [P] `src/agents/Payment.Agent/GlobalUsings.cs` — global using'ler (Microsoft.Agents.AI,
  Microsoft.Extensions.AI, ModelContextProtocol.Client, A2A).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Tüm story'lerin dayandığı çekirdek — `PaymentSession` aggregate + MCP server host +
Payment.Agent A2A host. **⚠️ Bu faz bitmeden hiçbir story başlayamaz.**

- [X] T005 `src/services/Payment.Api/Domains/PaymentSessions/PaymentSession.cs` — TÜM aggregate:
  - `enum`/`Enumeration` **`PaymentSessionStatus`**: `Opened`, `QuoteProvided`,
    `InstallmentSelected`, `Failed`.
  - VO **`OfferedInstallment`** (`InstallmentCount`, `UserTotalAmount`, `MonthlyAmount`) — POS/banka
    taşımaz (SC-004).
  - Aggregate **`PaymentSession : AggregateRoot`**: private setter; alanlar `CardToken`,
    `CartAmount`, `Status`, `_offeredInstallments` (private list, readonly expose),
    `SelectedInstallmentCount` (int?), `FailReason` (string?).
  - Davranışlar (invariant'lar burada, exception değil `ResultDomain`):
    `static Create(token, cartAmount)` (`cartAmount > 0` yoksa reddet, Status=`Opened`);
    `OfferInstallments(IEnumerable<OfferedInstallment>)` (Status `Opened` olmalı; boş liste →
    `Fail`; her satır `UserTotalAmount == CartAmount` — Model A invariant, aksi ihlal;
    Status→`QuoteProvided`); `SelectInstallment(int count)` (Status `QuoteProvided`|`InstallmentSelected`;
    `count` ⊂ `_offeredInstallments` değilse reddet; Status→`InstallmentSelected`); `Fail(reason)`
    (Status→`Failed`). (data-model.md CR-1..CR-6.)
- [X] T006 `src/services/Payment.Api/Program.cs` — MCP server host: `AddMcpServer()
  .WithHttpTransport(o => o.Stateless = true).WithToolsFromAssembly()` + `app.MapMcp("/mcp")`.
  **Marten kaydı**: önce mevcut aggregate'lerin (`PosAccount`, `BinCard`) kayıt biçimine bak —
  Marten document'leri kullanımda auto-register ediyorsa `PaymentSession` için ek kayıt GEREKMEZ
  (doğrula, ekleme yapma). Yalnız explicit `StoreOptions` konvansiyonu varsa (`RegisterDocumentType`
  / identity/index) `PaymentSession`'ı aynı biçimde ekle. (U2 — koşul netleştirildi.)
- [X] T007 [P] `src/agents/Payment.Agent/McpToolProvider.cs` — ECommerce deseni: `McpClient.CreateAsync(
  new HttpClientTransport(new(){ Name="payment", Endpoint=http://payment-api/mcp,
  TransportMode=StreamableHttp }))` → `ListToolsAsync()` → `IList<AITool>`. (Auth yok → PerUserMcpTool
  gerekmez; ham `McpClientTool : AIFunction` doğrudan kullanılır.)
- [X] T008 [P] `src/agents/Payment.Agent/PaymentAgentCard.cs` — `AgentCard`: name `PaymentAgent`,
  url, version `0.7.0`, `capabilities.streaming`, `defaultInput/OutputModes=["text"]`, 3 skill
  (`quote-installments`, `select-installment`, `payment-status`). PAN/CVV/expiry alanı YOK
  (SC-006). `pay-with-token` YOK. (contracts/agent-card.md.)
- [X] T009 [P] `src/agents/Payment.Agent/PaymentAgentPrompts.cs` — router instructions: "Ödeme
  yönlendiricisisin. Yalnız tool sırasını kur: önce `get_installment_options`, kullanıcı taksit
  seçince `select_installment`. Tutar/banka/kart ÜRETME — session'dan gelir. Çekim YOK." (FR-003, R3.)
- [X] T010 `src/agents/Payment.Agent/Program.cs` — host wiring (T007-T009'a bağlı):
  `AddServiceDefaults`; chat client (`AddOpenAIClientFromConfiguration("chat").AddChatClient("chat")`);
  `AIAgent` (`chatClient.CreateAIAgent(instructions, name, tools: mcpTools)`); A2A yayını
  `AddA2AServer(agent)` + `MapA2AHttpJson`/`MapA2AJsonRpc` + `MapWellKnownAgentCard(agentCard)`
  (`/.well-known/agent-card.json`). `[Experimental]` diagnostikleri suppress. (R1, R4.)
- [X] T011 `src/aspire/AppHost/AppHost.cs` — `payment-agent` projesini ekle; chat resource
  (`AddGitHubModel("chat","openai/gpt-4o-mini")` dev); `payment-agent`'a `WithReference(paymentApi)`
  (MCP için `http://payment-api`) + `WithReference(chat)`. (R5.)

**Checkpoint**: Aggregate + MCP host + A2A agent host hazır; MCP tool listesi henüz boş — story'ler
tool + slice ekler.

---

## Phase 3: User Story 1 — Taksit seçeneklerini getir (quote) (Priority: P1) 🎯 MVP

**Goal**: Token + sepet tutarından Model A taksit listesi üret, oturumu aç (`QuoteProvided`).

**Independent Test**: Geçerli kredi-kart token'ı + tutar ile `get_installment_options` çağır;
dönen listede yalnız desteklenen taksitler, her satır `userTotalAmount == cartAmount`,
`monthlyAmount == cartAmount / n`; banka kartı → yalnız peşin; boş → `Failed`.

- [X] T012 [P] [US1] `src/services/Payment.Api/CardVault/ICardVault.cs` — seam:
  `Task<Result<CardInfo>> ResolveCardInfoAsync(string token, CancellationToken ct)`. `CardInfo` =
  mevcut domain tipi (`Domains/Payments/BankRouter.cs`). Server-side; PAN yok. (FR-006, R7.)
- [X] T013 [US1] `src/services/Payment.Api/CardVault/SimulatedCardVault.cs` — `ICardVault` impl:
  token → BIN eşlemesini simüle et; BIN → `CardInfo` **008'in** `ResolveBinCard.Resolve(session, bin)`
  ile çöz (CP.VPOS DEĞİL); çözülemezse/geçersiz token → `Result` hata (kart verisi sızmadan, FR-019).
  `Program.cs`'te DI kaydı (`AddScoped<ICardVault, SimulatedCardVault>`).
  **Somut test-token seti** (quickstart S1/AC-3 için ZORUNLU — BIN'ler 008 kataloğunda GERÇEKTEN
  bulunmalı, aksi halde `ResolveBinCard` null döner):
  - `tok_credit_taksitli` → taksit destekleyen bir **kredi kartı** BIN'i (ör. Bonus/World/Maximum
    programlı; katalogdan doğrula) → quote taksitli liste döner.
  - `tok_debit` → bir **banka kartı** BIN'i (`CardType != Credit`) → yalnız peşin (AC-3).
  - `tok_invalid` (veya haritada olmayan herhangi token) → `Result` hata (FR-019).
  Not: gerçek token→PAN/BIN çözümü ayrı tokenizasyon feature'ı; bu tablo yalnız 007 elle-doğrulama
  için sabit fixture. (U1 — token seti tanımlandı.)
- [X] T014 [US1] `src/services/Payment.Api/Domains/PaymentSessions/Features/Agent/QuoteInstallmentsForSession.cs`
  — slice (record `QuoteInstallmentsForSessionCommand(cardToken, cartAmount)` + Response
  `{ sessionId, status, installments[] }` + Handler): `ICardVault` ile kartı çöz; desteklenen taksit
  sayılarını `PosAccount` komisyon gridinden türet (sabit liste YOK, FR-008); her taksit için
  `BankRouter` ile en ucuz destekleyen POS'u seç (yoksa o satır listeye girmez); **Model A** satırları
  üret (`UserTotalAmount = CartAmount`, `MonthlyAmount = Round(CartAmount/n,2)`, FR-010/011); banka
  kartı → yalnız `n=1` (FR-009); `PaymentSession.Create` + `OfferInstallments` (boş → `Fail`); persist
  (`IDocumentSession`, `[Transactional]`). **Saf, test edilebilir statik yardımcı** çıkar:
  `BuildOfferedInstallments(CardInfo, decimal cartAmount, IReadOnlyList<PosAccount>)` (DB'siz —
  T017/T018 bunu test eder).
- [X] T015 [US1] `src/services/Payment.Api/Domains/PaymentSessions/PaymentSessionMcpTools.cs` —
  `[McpServerToolType]` static class **`GetInstallmentOptionsMcpTool`**, `[McpServerTool(Name =
  "get_installment_options")]` + `[Description]`; parametreler `cardToken`, `cartAmount`; gövde
  `bus.InvokeAsync(new QuoteInstallmentsForSession...Command(...))`. (contracts/mcp-tools.md.)
- [X] T016 [P] [US1] `tests/Payment.Api.Tests/PaymentSessionCreateTests.cs` — `Create`:
  `cartAmount <= 0` reddi; başarıda Status=`Opened`.
- [X] T017 [P] [US1] `tests/Payment.Api.Tests/PaymentSessionOfferTests.cs` — `OfferInstallments`:
  boş liste → `Failed`; her satır `UserTotalAmount == CartAmount` invariant (aksi reddi);
  Status→`QuoteProvided`.
- [X] T018 [P] [US1] `tests/Payment.Api.Tests/QuoteModelATests.cs` — saf `BuildOfferedInstallments`:
  Model A tutar (`userTotal == cartAmount`, sapma 0, SC-002); `monthlyAmount` yuvarlama;
  desteklenmeyen taksit listede yok (SC-003); banka kartı → yalnız peşin (AC-3).

**Checkpoint**: US1 tek başına çalışır ve MCP `get_installment_options` ile test edilebilir (MVP).

---

## Phase 4: User Story 2 — Taksit seçimini oturuma kaydet (select) (Priority: P1)

**Goal**: Sunulan listeden seçilen taksiti oturuma yaz (`InstallmentSelected`). **Çekim yok.**

**Independent Test**: US1'den dönen `sessionId` + sunulan bir taksitle `select_installment` çağır;
`status = InstallmentSelected`, `selectedInstallmentCount` yazıldı. Sunulmayan taksit / quote'suz
oturum reddedilir.

- [X] T019 [US2] `src/services/Payment.Api/Domains/PaymentSessions/Features/Agent/SelectInstallment.cs`
  — slice (record `SelectInstallmentCommand(sessionId, installmentCount)` + Response `{ sessionId,
  status, selectedInstallmentCount }` + Handler): oturumu `sessionId` ile yükle; `PaymentSession
  .SelectInstallment(count)` (⊂ sunulanlar + faz guard, FR-012/017); persist. Çekim tetiklenmez,
  `Payment` kaydı oluşmaz (seam — plan §Seam).
- [X] T020 [US2] `PaymentSessionMcpTools.cs`'e ekle — `[McpServerToolType]` **`SelectInstallmentMcpTool`**,
  `[McpServerTool(Name = "select_installment")]`; parametreler `sessionId`, `installmentCount`; gövde
  `bus.InvokeAsync(new SelectInstallmentCommand(...))`. *(T015 ile aynı dosya — sıralı, [P] değil.)*
- [X] T021 [P] [US2] `tests/Payment.Api.Tests/PaymentSessionSelectTests.cs` — `SelectInstallment`:
  `count` ⊄ sunulanlar reddi (FR-012); quote'suz (`Opened`) oturuma select reddi (FR-017);
  başarıda Status→`InstallmentSelected` + `SelectedInstallmentCount` set; tekrar select
  öngörülebilir/idempotent (çift faz geçişi yok, FR-018).

**Checkpoint**: US1 + US2 birlikte iki fazlı akışı tamamlar (quote → select).

---

## Phase 5: User Story 3 — Ödeme oturumu durumunu sorgula (status) (Priority: P2)

**Goal**: Oturumun güncel fazını döndür.

**Independent Test**: Bir oturum aç, `payment_status(sessionId)` çağır; her faz geçişinden sonra
doğru durum döner.

- [X] T022 [US3] `src/services/Payment.Api/Domains/PaymentSessions/Features/Agent/GetPaymentSessionStatus.cs`
  — slice (record `GetPaymentSessionStatusQuery(sessionId)` + Response `{ sessionId, status,
  selectedInstallmentCount?, failReason? }` + Handler, `IQuerySession`): oturumu yükle, fazı döndür.
  Bulunamazsa `Result` NotFound. (contracts/mcp-tools.md.)
- [X] T023 [US3] `PaymentSessionMcpTools.cs`'e ekle — `[McpServerToolType]` **`PaymentStatusMcpTool`**,
  `[McpServerTool(Name = "payment_status")]`; parametre `sessionId`; gövde `bus.InvokeAsync(new
  GetPaymentSessionStatusQuery(...))`. *(T015/T020 ile aynı dosya — sıralı, [P] değil.)*

**Checkpoint**: 3 story de bağımsız çalışır; A2A akışı uçtan uca sorgulanabilir.

---

## Phase 6: Polish & Cross-Cutting

- [X] T024 [P] SC-006 gözden geçirme: `PaymentSessionMcpTools` tool input'ları + `PaymentAgentCard`
  şemasında tam PAN/CVV/expiry alanı **olmadığını** doğrula (yalnız `cardToken` + tutar + taksit).
- [X] T025 [P] `CLAUDE.md` güncelle: `src/agents/` klasörü + `Payment.Agent` (A2A host, BC değil) +
  Payment.Api MCP server / `Features/Agent` deseni notu.
- [X] T026 `dotnet build` (tüm `PaymentGateway.slnx` yeşil) + `dotnet test tests/Payment.Api.Tests`
  (tüm domain testleri yeşil).
- [X] T027 `quickstart.md` S1-S4 senaryolarını Aspire ile elle doğrula (agent card 3 skill, `/mcp`
  tool listesi 3 tool, quote Model A, select, status, güvenlik sınırı). Sonucu quickstart'a işaretle.

---

## Dependencies & Execution Order

### Phase bağımlılıkları
- **Setup (P1)**: bağımsız, hemen başlar.
- **Foundational (P2)**: Setup'a bağlı — TÜM story'leri bloklar (aggregate + MCP host + A2A host).
- **User Stories (P3-P5)**: Foundational bitince başlar. Öncelik sırası US1 → US2 → US3.
- **Polish (P6)**: istenen story'ler bitince.

### Story bağımlılıkları
- **US1 (P1)**: Foundational'dan sonra; başka story'ye bağlı değil. MVP.
- **US2 (P1)**: Foundational'dan sonra. Bağımsız test edilebilir ama pratikte US1'in ürettiği
  `sessionId` ile denenir (veri akışı, kod bağı değil).
- **US3 (P2)**: Foundational'dan sonra; bağımsız.

### Dosya-içi sıralama (aynı dosya → [P] değil)
- `PaymentSessionMcpTools.cs`: T015 → T020 → T023 (sıralı düzenleme).
- `Program.cs` (Payment.Api): T006 (MCP host) ve T013 (vault DI) aynı dosyaya dokunur → sıralı.

### Paralel fırsatlar
- Setup: T002, T003, T004 [P].
- Foundational: T007, T008, T009 [P] (Payment.Agent ayrı dosyalar); T010 bunlara bağlı.
- US1 testleri: T016, T017, T018 [P]. T012 [P] (vault seam ayrı dosya).
- US2 testi T021 [P]. Polish: T024, T025 [P].

---

## Parallel Example: Foundational agent host

```bash
# Payment.Agent bağımsız dosyaları birlikte:
Task: "McpToolProvider.cs — MCP client + ListToolsAsync"      # T007
Task: "PaymentAgentCard.cs — AgentCard 3 skill"               # T008
Task: "PaymentAgentPrompts.cs — router instructions"          # T009
# Sonra T010 (Program.cs) bunları birleştirir.
```

## Parallel Example: User Story 1 tests

```bash
Task: "PaymentSessionCreateTests.cs"    # T016
Task: "PaymentSessionOfferTests.cs"     # T017
Task: "QuoteModelATests.cs"             # T018
```

---

## Implementation Strategy

### MVP First (US1)
1. Phase 1 Setup → 2. Phase 2 Foundational (KRİTİK, tüm story'leri bloklar) →
3. Phase 3 US1 → 4. **DUR & DOĞRULA**: US1'i MCP `get_installment_options` + domain testleriyle
bağımsız test et → 5. Model A doğrulandı mı? (session şüphesi — bkz. Obsidian `Yapılacaklar`).

### Incremental
1. Setup + Foundational → temel hazır.
2. US1 (quote) → test → MVP.
3. US2 (select) → test → iki fazlı akış tam.
4. US3 (status) → test.
5. Polish + quickstart elle doğrulama.

---

## Notes

- [P] = farklı dosya, bağımlılık yok. [Story] = US1/US2/US3 izlenebilirlik.
- Preview paketler (A2A/Agent Framework/MCP hariç) — sürümler pin; `[Experimental]` suppress.
- Test yalnız saf domain (`PaymentSession` + Model A statik hesap). A2A/MCP/LLM = quickstart elle.
- **Ertelenen**: fiili çekim (`ProcessPayment` yeniden kurgu), `Awaiting3D`/`Completed` fazları,
  `Payment` kaydı — 007 dışı, seçilen taksiti seam'e bırakır.
- **Açık soru**: `PaymentSession` gerekliliği (merchantId/stateless-offer alternatifi) — şimdilik
  session ile devam, sonra dönülecek (Obsidian `Yapılacaklar` + memory).
- **Ertelenmiş güvenlik riski (anayasa V — C2)**: `PaymentSession` yalnız `sessionId` (Guid) ile
  erişilir; tenant/merchant bağı YOK → sessionId'yi bilen select/status yapabilir (bearer-capability).
  Proje-geneli AUTHZ ertelemesi kapsamında kabul; Guid tahmin-edilemezliği kısmi azaltma. İleride
  `SessionIsolationKeyProvider` (claims-tabanlı, contracts/a2a-task-flow.md) + tenant filtresi
  takılmalı. 007 kapsamında değil ama açık risk olarak izlenir (Identity BC gelince). Merchant bağı
  eklenirse session açık sorusu da (merchantId) burada çözülebilir.
---

## Uygulama notları (2026-08-03 implement — plandan küçük sapmalar)

- **T006 Marten**: mevcut kayıt biçimi explicit (`opts.Schema.For<...>()`); `PaymentSession` aynı
  biçimde eklendi (`Program.cs`). Auto-register değil — U2 koşulu "explicit varsa ekle" gerçekleşti.
- **T010 chat client**: plandaki `AddOpenAIClientFromConfiguration("chat")` yerine **ECommerce
  ChatAgent deseni** kullanıldı — `OpenAIClient` (`OpenAI:ApiKey`/`OpenAI:Model`/opsiyonel
  `OpenAI:Endpoint`) → `AsIChatClient()` → `new ChatClientAgent(...)`. Kanıtlı, çalışan desen.
  A2A yayını: `MapA2AJsonRpc` + `MapWellKnownAgentCard` (sample'da doğrulanan API; `MapA2AHttpJson`
  kullanılmadı).
- **T011 chat resource**: plandaki Aspire `AddGitHubModel("chat")` resource'u **eklenmedi** —
  ECommerce'de olduğu gibi chat anahtarı agent'ın kendi config'inden gelir (user-secrets). AppHost'a
  yalnız `payment-agent` + `WithReference(paymentApi)` eklendi. `Aspire.Hosting.GitHub.Models` paketi
  gereksiz kaldığından `Directory.Packages.props`'tan çıkarıldı. (İleride GitHub Models resource'u
  istenirse eklenebilir.)
- **T013 DI**: manuel `AddScoped<ICardVault, SimulatedCardVault>` yerine `IScopedDependency` marker
  (Scrutor auto-register) — proje konvansiyonu. Test token'ları: `tok_credit_taksitli`→BIN 365770
  (kredi/Bonus), `tok_debit`→BIN 401049 (banka kartı); ikisi de bincards.json'da doğrulandı.
- **Doğrulama**: `dotnet build PaymentGateway.slnx` = 0 hata; `dotnet test tests/Payment.Api.Tests`
  = **64/64 yeşil** (48 mevcut + 16 yeni PaymentSession/Model A). A2A/MCP/LLM uçtan uca = T027
  (Aspire elle — Docker + chat anahtarı gerektirir, bu oturumda çalıştırılmadı).
