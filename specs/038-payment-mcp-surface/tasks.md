# Tasks: Ödeme Süreci A2A + MCP Üzerinden (038)

**Input**: Design documents from `/specs/038-payment-mcp-surface/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Anayasa gereği yalnız saf domain birim testleri; bu işin yeni kodu ağırlıkla
handler/agent düzeyinde (test edilmez — quickstart canlı senaryolarıyla doğrulanır). Bu
yüzden ayrı test görevi yok; her story kendi canlı doğrulama göreviyle kapanır.

**Organization**: Görevler user story bazında; PG = PaymentGateway reposu (bu repo kökü),
EC = `/Users/macbook/Desktop/ECommerceWithAgentFramework/`.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: /mcp yüzeyinin ve agent konfigürasyonunun iskeleti

- [X] T001 PG: Payment.Api'ye MCP server kaydı — `src/services/Payment.Api/Payment.Api.csproj`'a sürümsüz `ModelContextProtocol.AspNetCore` PackageReference (sürüm CPM'de; yoksa `Directory.Packages.props`'a ekle) + `src/services/Payment.Api/Program.cs`'e `AddMcpServer().WithHttpTransport().WithToolsFromAssembly()` ve `MapMcp("/mcp").RequireAuthorization("payment.write" policy — 011 Merchant.Api deseni)` kaydı
- [X] T002 [P] PG: Payment.Agent config gözden geçirme — `src/agents/Payment.Agent/Program.cs` + Options POCO'ları: Payment.Api /mcp adresi Aspire service discovery'den, Identity adresi + client bilgisi mevcut `AgentTokenHandler` düzeninde; magic-string config yasak (Options pattern), eksik bölüm varsa `Options/` altında POCO + `AddOptionsExt`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Çekim statü kapısının veri temeli — US2 başlamadan bitmeli (US1'i bloklamaz ama
aynı Program.cs/Wolverine kablolamasına dokunduğu için önce alınır)

- [X] T003 PG: `src/services/Payment.Api/Domains/MerchantStatus/MerchantStatusReference.cs` — doküman tipi (aggregate DEĞİL): `Id` (=MerchantId), `Status` (string, event'teki değer aynen), `UpdatedAtUtc` (data-model.md)
- [X] T004 PG: `src/services/Payment.Api/Domains/MerchantStatus/MerchantLifecycleEventHandler.cs` — Wolverine tüketici: `public static class` + `public static async Task Handle(MerchantCreated ...)` ve `Handle(MerchantStatusChanged ...)`, idempotent upsert (`IDocumentSession`); sınıf adı TEKİL "Handler" (çoğul sessizce keşfedilmez!); şablon: Identity.Server `MerchantClientEventHandler`
- [X] T005 PG: `src/services/Payment.Api/Program.cs` — Wolverine'e `merchant.lifecycle` fanout dinleme kaydı (durable queue, `ProcessInline` — message store yok; Identity.Server Program.cs deseni); Shared kontrat referansının (`MerchantCreated`/`MerchantStatusChanged`) projede olduğunu doğrula, yoksa `src/others/Shared` ProjectReference ekle

**Checkpoint**: Payment.Api derlenir; Aspire'da ayağa kalkınca merchant statü değişimi log'da "Successfully processed message" üretir

---

## Phase 3: User Story 1 - A2A üzerinden taksit sorgusu (Priority: P1) 🎯 MVP

**Goal**: Chat'ten taksit sorusu → A2A → Payment.Agent → PG /mcp → taksit listesi; EC köprüsüz

**Independent Test**: quickstart S1 — sepet + "taksit seçeneklerini göster" → doğru seçenek listesi (test kartlarıyla karşılaştırmalı)

- [X] T006 [US1] PG: `src/services/Payment.Api/Domains/Payments/Features/Agents/InstallmentOptionsForAgent.cs` — Agent slice: `record InstallmentOptionsForAgentQuery(Guid MerchantId, string VaultToken, decimal Amount)` + `InstallmentOptionsView` + Handler: StoredCard'ı VaultToken+MerchantId ile çöz (yok/Revoked → `MessageItem` ret), kartın Bin'iyle iyzico taksit sorgusu (wire tipleri SLICE İÇİNE nested — mevcut `Features/Queries/InstallmentOptions.cs`'ten kopya, 037 kuralı: non-user literal'ler `IyzicoRequestOptions`'tan); Commands/Queries slice'ına GİTMEZ (kod tekrarı bilinçli)
- [X] T007 [US1] PG: `src/services/Payment.Api/Domains/Payments/PaymentMcpTools.cs` — `[McpServerTool(Name = "get_installment_options")]`: yalnız `InstallmentOptionsForAgent`'ı `IMessageBus.InvokeAsync` ile çağırır (contracts/mcp-payment-tools.md sözleşmesi); aggregate kökünde durur
- [X] T008 [US1] PG: `src/agents/Payment.Agent/McpToolProvider.cs` + `Program.cs` — Payment.Api /mcp'ye bağlanıp `get_installment_options` tool'unu keşfet/allowlist'e al; `AgentTokenHandler` makine token'ı taşır
- [X] T009 [US1] PG: `src/agents/Payment.Agent/ConstValues.cs` — RouterInstructions: vault token + tutar girdisinde `get_installment_options` çağır; LLM tutar/kart/taksit ÜRETMEZ (007 kuralı); `src/agents/Payment.Agent/PaymentAgentCard.cs` `quote-installments` skill açıklamasını canlı akışa göre güncelle (`installment_quote` BIN skill'i AYNEN kalır)
- [X] T010 [P] [US1] EC: `src/services/customer/Customer.Api/Domains/Wallets/Features/Agents/GetPaymentContextForAgent.cs` — YENİ Agent slice: müşterinin varsayılan (veya `cardId` parametresiyle seçilen) kartının vault token'ı + buyer bilgisi + sepet kalem özeti İSTENEN alanlarla tek yanıtta (R3; charge bağlamının tamamı — US2 de bunu kullanacak); `src/services/customer/Customer.Api/Domains/Wallets/SavedCardPaymentMcpTools.cs`'e `get_payment_context` MCP tool'u ekle (yalnız bu slice'ı çağırır)
- [X] T011 [US1] EC: `src/agents/ChatAgent/ConstValues.cs` + `Program.cs` — assistant persona kural 8 yeniden: taksit = `get_basket` → `get_payment_context` → A2A `quote-installments` isteği (contracts/a2a-payment-agent.md `installments` payload'ı); A2A çağrısı mevcut `a2a-payment` named-client + SendMessage düzeniyle (019/024 deseni); `get_card_installments` çağrısı yönergeden çıkar
- [ ] T012 [US1] Canlı doğrulama S1 (quickstart.md) — iki AppHost + sandbox kartlarıyla taksit listesi; EC log'unda PG'ye HTTP çekim çağrısı YOK

**Checkpoint**: US1 tek başına gösterilebilir (MVP)

---

## Phase 4: User Story 2 - A2A üzerinden kayıtlı kartla çekim (Priority: P2)

**Goal**: Onay sonrası gerçek çekim A2A + /mcp zinciriyle; EC eski yolu SÖKÜLÜR (Q1=A)

**Independent Test**: quickstart S2 (çekim) + S3 (statü kapısı fail-closed)

- [X] T013 [US2] PG: `src/services/Payment.Api/Domains/Payments/Features/Agents/ChargeSavedCardForAgent.cs` — Agent slice: `record ChargeSavedCardForAgentCommand(Guid MerchantId, string VaultToken, decimal Amount, decimal PaidPrice, int Installment, BuyerInput Buyer, List<BasketItemInput> BasketItems)` + `ChargeResultView` + `[Transactional]` Handler sırası (contracts/mcp-payment-tools.md): (1) `MerchantStatusReference` oku — yok/≠"Active" → fail-closed RET (iyzico'ya gidilmez), (2) StoredCard çöz — yok/Revoked → RET, (3) iyzico çekim (wire SLICE İÇİNE nested, `Features/Commands/ChargePayment.cs`'ten kopya; literal'ler `IyzicoRequestOptions`), (4) başarıda Payment kaydı + mevcut event akışı, başarısızda Failed kaydı (033 deseni)
- [X] T014 [US2] PG: `src/services/Payment.Api/Domains/Payments/PaymentMcpTools.cs` — `[McpServerTool(Name = "charge_saved_card")]` ekle: yalnız `ChargeSavedCardForAgent`'ı çağırır
- [X] T015 [US2] PG: `src/agents/Payment.Agent/ConstValues.cs` + `PaymentAgentCard.cs` — RouterInstructions'a çekim akışı (charge girdisi → `charge_saved_card`; değer üretme yasağı aynen) + AgentCard'a `charge_saved_card` skill'i (contracts/a2a-payment-agent.md); "çekim yoktur" notlarını kaldır, "PAN/CVV kabul etmez" kuralı KALIR; `McpToolProvider` allowlist'ine tool'u ekle
- [X] T016 [US2] EC: `src/agents/ChatAgent/ConstValues.cs` + `Program.cs` — persona kural 9 yeniden: çekim = seçenek göster → AÇIK ONAY → A2A `charge` isteği (payload: `get_payment_context` çıktısı VERBATIM — buyer/basketItems LLM üretmez/göstermez); başarı/başarısızlık mesaj kuralları (teknik ayrıntı sızmaz)
- [X] T017 [US2] EC SÖKÜM: `src/services/customer/Customer.Api/Domains/Wallets/SavedCardPaymentMcpTools.cs`'ten `get_card_installments` + `charge_default_card` tool'ları, `Features/Agents/GetCardInstallments.cs` + `Features/Agents/ChargeDefaultCard.cs` slice'ları ve PG'ye giden HTTP çekim köprüsü (typed client/named client kayıtları dahil) SİLİNİR; `src/agents/ChatAgent/ConstValues.cs`'ten `CustomerTools.GetCardInstallments/ChargeDefaultCard` sabitleri ve tool kayıtları çıkar (`get_default_card_bin` + `get_cards` KALIR); EC çözümü 0 hata derlenir
- [ ] T018 [US2] Canlı doğrulama S2 + S3 (quickstart.md) — sandbox çekim (İş Maximum 3 taksit) + iyzico panel eşleşmesi; merchant Passive → çekim gateway içinde RET (PG log'unda sağlayıcı çağrısı yok), Active'e dönünce geçer

**Checkpoint**: Uçtan uca ödeme tek yol (A2A) üzerinden; eski köprü yok

---

## Phase 5: User Story 3 - Seçilen kartla işlem (Priority: P3)

**Goal**: Kart listesi/seçim EC cüzdanında; seçilen kartın token'ı A2A'ya taşınır; PG'de kod değişikliği YOK

**Independent Test**: quickstart S4 — iki kartlı müşteri, ikinci kartla taksit/çekim

- [X] T019 [US3] EC: `src/agents/ChatAgent/ConstValues.cs` + `Program.cs` — persona kart-seçim akışı: "kartlarımı göster" → cüzdan `get_cards` (maskeli liste); "şu kartımla" → seçilen kartın kimliği `get_payment_context(cardId)` çağrısına, dönen vault token A2A isteğine; kart LİSTESİ A2A'ya asla gönderilmez
- [X] T020 [US3] EC: `src/services/customer/Customer.Api/Domains/Wallets/Features/Agents/GetPaymentContextForAgent.cs` — `cardId` parametre yolunu doğrula/tamamla (T010'da eklendi; seçilen kart müşteriye ait değilse ret — fail-closed)
- [ ] T021 [US3] Canlı doğrulama S4 (quickstart.md) — ikinci kartla taksit farkı gözlenir; PG log'unda kart listeleme isteği YOK

**Checkpoint**: Kart seçimi çalışır; PG dokunulmadı

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T022 [P] EC: persona güvenlik satırı — chat'ten kart EKLEME/SİLME istekleri reddedilir, ekran yoluna yönlendirilir (`src/agents/ChatAgent/ConstValues.cs`; güvenlik kararı 2026-08-16)
- [ ] T023 Canlı doğrulama S5 (quickstart.md) — güvenlik denetimleri: kart ekleme reddi; S1/S2 yanıtlarında PAN/CVC/cardUserKey/cardToken taraması; /mcp token'sız 401; SC-005 kod denetimi: iki repoda grep ile Payment.Api /mcp'ye bağlanan TEK yerin Payment.Agent olduğunu doğrula (Merchant.Api onboarding /mcp tüketimi — 032 — kapsam dışı, dokunulmaz)
- [X] T024 [P] PG: `CLAUDE.md` güncelle — Payment.Api /mcp dirilişi (2 tool + Agents slice'ları), `Domains/MerchantStatus/` event-fed referans, Payment.Agent canlı skill seti; `README.md`'de akış şeması varsa A2A ödeme zinciri işlenir
- [X] T025 [P] EC: `README`/persona dokümantasyonunda ödeme akışının A2A'ya taşındığı not edilir (EC repo konvansiyonuna göre)
- [ ] T026 İki repo final: PG `dotnet build` + `dotnet test` (47+ test yeşil), EC `dotnet build` (+ testleri varsa) 0 hata; quickstart S1-S5 özet turu

---

## Dependencies

```
Phase 1 (T001-T002) ──> Phase 2 (T003-T005) ──> US1 (T006-T012) ──> US2 (T013-T018) ──> US3 (T019-T021) ──> Polish (T022-T026)
```

- T003→T004→T005 sıralı (aynı klasör/Program.cs).
- US1 içinde: T006→T007 (slice önce tool sonra); T008-T009 T007'den sonra; T010 [P] bağımsız (EC, farklı repo); T011 T009+T010'dan sonra; T012 hepsinden sonra.
- US2: T013→T014→T015 (PG zinciri); T016 T010'a dayanır; T017 söküm T016'dan SONRA (persona yeni yola geçmeden eskisini kırma); T018 hepsinden sonra.
- US3: T019-T020 paralel değil (T020, T010'un parametre yolu); T021 son.
- US2, US1'in Payment.Agent/persona zeminini kullanır — US1 bitmeden başlamaz (kod dosyaları ortak).

## Parallel Execution Examples

- T002 ∥ T001 (farklı projeler).
- US1: T010 (EC) ∥ T006-T009 (PG) — farklı repo, bağımsız dosyalar.
- Polish: T022 ∥ T024 ∥ T025.

## Implementation Strategy

**MVP = Phase 1 + 2 + US1 (T001-T012)**: para riski olmadan zincirin tamamı kanıtlanır.
Sonra US2 (çekim + söküm) tek parça teslim edilir — söküm (T017) persona geçişinden (T016)
sonra ki eski yol hiç kırılmadan yenisi devreye girsin. US3 küçük EC-only artım. Her story
kendi canlı doğrulamasıyla kapanır (sandbox-only kural).