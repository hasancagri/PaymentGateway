# Tasks: Komisyon Teklifi ve Metin-Sürümlü Pazarlık

**Input**: Design documents from `/specs/019-commission-proposal-acceptance/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/README.md, quickstart.md

**Tests**: Saf domain birim testleri dahil (house-style: aggregate davranışları test edilir;
handler/HTTP/agent akışı quickstart ile elle doğrulanır).

**Organization**: Faz 1 setup, Faz 2 foundational (mesaj kontratı + MCP altyapısı), sonra user
story fazları (P1: US1 teklif, US2 kabul, US3 ret+revizyon, US4 değişmezlik; P2: US5 görünürlük),
son faz temizlik (Finalize söküm) + polish.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [X] T001 `Directory.Packages.props`'a `ClosedXML` sürümü ekle (CPM); `src/others/Mail.Worker/Mail.Worker.csproj`'a sürümsüz `PackageReference` ekle
- [X] T002 [P] `src/services/Commission.Api/Options/CommissionProposalOption.cs` oluştur: `DefaultMarginPoints:decimal` (Required), `TicketTtlHours:int` (Required), `PublicBaseUrl:string` (Required); `AddOptionsExt` uzantısında bağla (BindConfiguration + ValidateDataAnnotations + ValidateOnStart + POCO unwrap); `appsettings.Development.json`'a örnek bölüm; `Program.cs`'e kayıt
- [X] T003 [P] Identity seed güncelle: `merchant-agent` client'ına `commission.write` scope ekle (`src/others/Identity.Server` seed; idempotent kalmalı)

## Phase 2: Foundational (mesaj kontratı + MCP altyapısı — story'lerden önce)

- [X] T004 `src/others/Shared/IntegrationEvents.cs`: `EmailAttachmentTable(FileName, Headers, Rows)` record'u + `SendEmailRequested`'e opsiyonel `Attachment` parametresi ekle (contracts §3)
- [X] T005 `src/others/Mail.Worker/SendEmailHandler.cs`: `Attachment != null` ise ClosedXML ile Headers+Rows'tan xlsx üret, `System.Net.Mail.Attachment` olarak ekle; retry/dead-letter davranışına dokunma
- [X] T006 `src/services/Commission.Api/Program.cs`: `AddMcpServer().WithToolsFromAssembly()` + `MapMcp("/mcp").RequireAuthorization(AuthorizationScopes.CommissionWrite)` (Payment.Api deseni)
- [X] T007 [P] `src/agents/Merchant.Agent`: Commission.Api `/mcp` için ikinci MCP client — `McpToolProvider.cs`'e Commission tool'ları, `Options/`'a Commission MCP adres alanı, `appsettings.Development.json` güncelle (Aspire service discovery adresi AppHost'tan)
- [X] T008 [P] `src/aspire/AppHost/AppHost.csproj` host dosyası: Merchant.Agent'a Commission.Api referansı/env bağla (service discovery)

## Phase 3: US1 — Metinle teklif sunma (P1) 🎯 MVP

**Goal**: "Kahve Dünyası'na ilk komisyon teklifimizi sun" → Excel ekli + 2 linkli mail kuyruğa.

**Independent Test**: quickstart S1.

- [X] T009 [US1] `src/services/Commission.Api/Domains/CommissionDrafts/CommissionDraft.cs`: aggregate + `DraftRow` VO (`ValueObjects/` altına) + `CreateFromBankGrid` (deterministik sıra: BankCode ASC → Installment ASC, 1-tabanlı RowNo; boş grid → Error; `ResultDomain<CommissionDraft>`; metotlara `<summary>` + `<remarks>Handler:</remarks>`)
- [X] T010 [US1] `src/services/Commission.Api/Domains/CommissionProposals/CommissionProposal.cs`: aggregate + `ProposalStatus` enum + `IssueFrom` / `Supersede` (data-model geçişleri; 014 Result sözleşmesi; handler notları)
- [X] T011 [US1] `.../CommissionProposals/Features/Agents/SubmitCommissionProposalForAgent.cs`: kendi Query/Command + Handler (`[Transactional]`, `IDocumentSession` doğrudan): Accepted-varsa RET; draft yoksa `BankCommission` + marj'dan `CreateFromBankGrid`; önceki Pending → `Supersede`; `IssueFrom` ile yeni teklif; `SendEmailRequested` publish (Body: kısa özet + mutlak Kabul/Ret linkleri `PublicBaseUrl`'den; Attachment: RowNo/Banka/Taksit/Oran tablosu)
- [X] T012 [US1] `.../CommissionProposals/CommissionProposalMcpTools.cs`: `submit_commission_proposal` tool'u (`[McpServerToolType]`, yalnız `IMessageBus.InvokeAsync` ile T011 slice'ı; contracts §1)
- [X] T013 [US1] `src/agents/Merchant.Agent/MerchantAgentCard.cs` + agent yönlendirme: `propose_commission` skill'i (`get_merchant` → `submit_commission_proposal` zinciri; LLM yalnız sıra kurar)
- [X] T014 [P] [US1] `tests/Commission.Api.Tests`: `CommissionDraftTests` — marj türetme, deterministik sıra/satır no, boş grid Error
- [X] T015 [P] [US1] `tests/Commission.Api.Tests`: `CommissionProposalTests` — IssueFrom fotoğraf + bilet üretimi, Supersede geçişi/idempotens

**Checkpoint**: quickstart S1 (Mailpit'te Excel ekli mail).

## Phase 4: US2 — Merchant kabulü: insansız zincir (P1)

**Goal**: Kabul linki tek tık → komisyon hücreleri + merchant Active; gateway'de sıfır insan işi.

**Independent Test**: quickstart S2.

- [X] T016 [US2] `CommissionProposal.Accept(now)` + `CommissionDraft.Lock()` davranışları (bilet: tek kullanım + TTL + yalnız Pending; data-model)
- [X] T017 [US2] `.../CommissionProposals/Features/Commands/AcceptCommissionProposal.cs`: GET onay sayfası (mini HTML, Türkçe) + POST handler (`[Transactional]`): ticket→proposal bul, `Accept`, draft `Lock`, satırları `MerchantCommission`'a kopyala, `MerchantCommissionGridReady` publish; sonuç/geçersiz-bilet sayfaları
- [X] T018 [US2] `.../CommissionProposals/CommissionProposalEndpointExtension.cs`: anonim karar uçları map (contracts §2; `Program.cs`'e grup kaydı — AllowAnonymous bilinçli, yetki=bilet)
- [X] T019 [P] [US2] `tests/Commission.Api.Tests`: Accept testleri — geçerli bilet Pending→Accepted, kullanılmış/TTL-dolmuş/Superseded → Error + durum değişmez; Lock idempotens

**Checkpoint**: quickstart S2 (tek tık → Active; ikinci tık geçersiz).

## Phase 5: US3 — Ret + metinle revizyon + yeniden gönder (P1)

**Goal**: Ret gerekçesi kayıt; "satır 37'yi 1.85 yap" → diff; "merchant'a gönder" → yeni tur.

**Independent Test**: quickstart S3.

- [X] T020 [US3] `CommissionProposal.Reject(reason, now)` davranışı (boş gerekçe Error; bilet kuralları)
- [X] T021 [US3] `.../CommissionProposals/Features/Commands/RejectCommissionProposal.cs`: GET gerekçe formu (mini HTML) + POST handler (`[Transactional]`); sonuç/geçersiz-bilet sayfaları; endpoint extension'a ekle
- [X] T022 [US3] `CommissionDraft.Revise(operations, bankFloorLookup)` davranışı: set (row / bank+installment / filter) + delta (filter), sunucu-tarafı hesap, taban bekçisi BÜTÜN-veya-hiç, geçersiz adres Error, kilitliyse Error, `ResultDomain<List<DraftChange>>` diff
- [X] T023 [US3] `.../CommissionDrafts/Features/Agents/ReviseCommissionDraftForAgent.cs`: kendi Command + Handler (`[Transactional]`): draft yükle, güncel `BankCommission` tabanlarını sözlüğe çek, `Revise`, diff Response döndür
- [X] T024 [US3] `CommissionProposalMcpTools`'a `revise_commission_draft` tool'u ekle (contracts §1 işlem şeması)
- [X] T025 [US3] Merchant.Agent: `revise_commission_draft` skill'i (talimat: YALNIZ admin'in açık değerleri; diff'i yankıla) + "merchant'a gönder" → `submit_commission_proposal` yeniden kullanım talimatı
- [X] T026 [P] [US3] `tests/Commission.Api.Tests`: Revise testleri — satır-no set, bank+installment set, filter set/delta, taban ihlali bütün-RET + draft değişmez, geçersiz satır Error, kilitli Error; Reject testleri — gerekçe kayıt, boş gerekçe Error

**Checkpoint**: quickstart S3 (diff yankısı; gönder'siz mail yok; gönder → yeni bilet, eski ölü).

## Phase 6: US4 — Kabul sonrası değişmezlik (P1)

**Goal**: Accepted sonrası revizyon + yeni teklif RET.

**Independent Test**: quickstart S4.

- [X] T027 [US4] Handler bariyerleri: `SubmitCommissionProposalForAgent` + `ReviseCommissionDraftForAgent`'ta MerchantId'ye ait Accepted teklif sorgusu → RET (IsLocked ile çifte bariyer; T011/T023 üzerinde doğrulama geçişi)
- [X] T028 [P] [US4] `tests/Commission.Api.Tests`: kilit senaryoları — Locked draft Revise Error; Accepted sonrası IssueFrom akışının handler-seviyesinde reddedildiğini domain kuralları üzerinden doğrula

**Checkpoint**: quickstart S4.

## Phase 7: US5 — Teklif durumu görünürlüğü (P2)

**Goal**: Agent'tan durum + taslak tablosu; Admin UI salt-okuma + teklif durumu.

**Independent Test**: quickstart S5.

- [X] T029 [P] [US5] `.../CommissionDrafts/Features/Agents/ShowCommissionDraftForAgent.cs`: satır no'lu tam tablo + IsLocked (kendi Query + Handler)
- [X] T030 [P] [US5] `.../CommissionProposals/Features/Agents/CommissionProposalStatusForAgent.cs`: son teklif durumu (None/Pending/Accepted/Rejected + gerekçe + zaman)
- [X] T031 [US5] `CommissionProposalMcpTools`'a `show_commission_draft` + `commission_proposal_status` tool'ları; Merchant.Agent'a iki skill (`show_commission_draft`, `commission_proposal_status`)
- [X] T032 [US5] `src/ui/Admin` komisyon ekranı: teklif durumu bölümü ekle (Commission.Api'ye küçük `Features/Queries/GetCommissionProposalStatus.cs` HTTP ucu `commission.read` ile + Admin typed HttpClient; yalnız gösterim, Türkçe `MessageText`)

**Checkpoint**: quickstart S5.

## Phase 8: Söküm + Polish

- [X] T033 FR-013 söküm: `src/services/Commission.Api/Domains/MerchantCommissions/Features/Commands/FinalizeMerchantCommissionGrid.cs` sil; `MerchantCommissionGrid` aggregate + `GridStatus` sil; endpoint extension + `Program.cs` kayıtlarını temizle (defansif migration YOK — dev DB sıfırlanır)
- [X] T034 `src/ui/Admin`: Finalize butonu + merchant komisyon upsert çağrılarını kaldır; grid salt-okuma (Commission.Api'deki upsert ucu admin-düzlem gereksinimi kalmadıysa kaldır/işaretle)
- [X] T035 [P] `dotnet build` 0 hata + `dotnet test tests/Commission.Api.Tests` ve `tests/Merchant.Api.Tests` yeşil; Wolverine handler adlarının TEKİL "Handler" bittiğini doğrula (çoğul tuzak)
- [ ] T036 Quickstart S1-S5 canlı doğrulama (Aspire + Mailpit; agent chat ile uçtan uca) — elle
- [X] T037 [P] `CLAUDE.md` güncelle: 019 özeti (CommissionDraft/CommissionProposal, Commission /mcp yüzeyi, SendEmailRequested Attachment, Finalize söküm) + anayasa PATCH amendment hatırlatması (BaseModel/Enumeration bayat atfı — research R7, ayrı iş)

## Dependencies

- Faz 1 → Faz 2 → US1 → US2 → US3 → US4 (bariyer doğrulama) → US5 → Söküm.
- US2, US1'in ürettiği teklife ihtiyaç duyar (bilet). US3, US2'nin ret koluna dayanır ama Reject
  ucu bağımsız yazılabilir (T020-T021, T016'dan bağımsız [P] değil — aynı aggregate dosyası).
- T033 sökümü en sona: Finalize, US2 kabul zinciri canlıya çıkana dek alternatif yol olarak
  kalırsa çakışma üretir — söküm US2 doğrulandıktan sonra.

## Parallel Opportunities

- Faz 1: T002 ∥ T003. Faz 2: T007 ∥ T008 (T006 sonrası).
- US1: T014 ∥ T015 (implementasyonla eşzamanlı, farklı dosyalar).
- US5: T029 ∥ T030.
- Polish: T035 ∥ T037.

## Implementation Strategy

**MVP = US1** (teklif maili Mailpit'te görünür — değer anında gösterilebilir). Sonra US2 (kabul
zinciri = feature'ın kalbi), US3 (pazarlık döngüsü), US4 (bariyer), US5 (görünürlük), söküm.
Her checkpoint quickstart senaryosuyla bağımsız test edilir.