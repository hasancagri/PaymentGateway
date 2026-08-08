# Implementation Plan: Merchant Onboarding — Agentic Kayıt + İnsan Onayı + Kademeli Yetki

**Branch**: `013-merchant-onboarding` | **Date**: 2026-08-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/013-merchant-onboarding/spec.md`

## Summary

Merchant adayı A2A üzerinden (yeni **Merchant.Agent**) başvurur; gateway aday sitenin
`/.well-known/merchant-descriptor.json` dosyasını okur ve HTTP-01 tarzı domain-control
challenge ile alan adı sahipliğini senkron doğrular. Geçerse başvuru **RegisterRequest**
olarak (merchant'tan AYRI, Pending) saklanır ve admin'e mail gider. Admin, Admin UI
"Merchant Talepleri" sayfasından onaylar → merchant O ANDA oluşur (MerchantKey üretilir),
aktivasyon maili gider. Aktivasyon sayfası **Identity.Server**'da barındırılır; tek
kullanımlık bilet doğrulanınca MerchantKey bir kez gösterilir ve merchant **Provisioning**
statüsüne geçer (sınırlı token; charge kapalı). Komisyon onay SONRASI, **gateway-otoriter**
belirlenir — merchant pazarlık/kabul/ret YAPMAZ (B kararı); admin grid'i tanımlar ve "grid
hazır" koşulu event ile Merchant BC'ye taşınır. Komisyon Excel'i için 013 **MCP yüzeylerini**
sağlar (Merchant.Api `get_merchant` → Commission.Api `get_merchant_commission_grid` →
Excel.Mcp `generate_spreadsheet` → Mail.Mcp `send_email`); bunları süren **harici LLM/MCP
client seçimi 013 dışı** (belirli araç bağlanmaz).
Settlement hesabı + komisyon-grid-hazır + ReturnUrl üçü tamamlanınca merchant OTOMATİK
**Active** olur (tam yetki).

**Teknik yaklaşım**: Mevcut desenlerin bileşimi — Payment.Agent şablonundan Merchant.Agent
(başvuru); Payment.Api `/mcp` deseninden Merchant.Api + Commission.Api MCP yüzeyleri; 012
merchant-istemci düzlemi + Identity.Server client-sync; Marten aggregate + Wolverine outbox
event akışı. Net-yeni parçalar: RegisterRequest aggregate, domain-control challenge, aktivasyon
bileti + Identity Razor sayfası, generic Mail.Mcp + Excel.Mcp, Merchant/Commission MCP read
tool'ları, Common IMailSender (yalnız deterministik mailler), Provisioning statüsü +
3-koşul→Active otomasyonu. **Sınır**: komisyon Excel maili = harici LLM+MCP orkestrasyon;
aktivasyon (tek-seferlik key linki) + admin bildirim = deterministik (IMailSender, LLM'e
verilMEZ); grid-hazır koşul event'i deterministik. **013 DIŞI** (→014): komisyon pazarlığı
(grid sürümleme + kabul + gelen-mail + ML-intent) — Obsidian `Yapılacaklar.md` vizyon maddesi.

## Technical Context

**Language/Version**: C# / .NET 10 (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Marten (event/document store), Wolverine (bus + RabbitMQ fanout),
.NET Aspire (orkestrasyon), OpenIddict (Identity.Server), Microsoft.Agents.AI +
A2A + ModelContextProtocol (Agent/MCP), Microsoft.Extensions.AI.OpenAI (LLM router),
ClosedXML (Excel.Mcp — .xlsx üretimi), CP.VPOS (ilgisiz — dokunulmaz). Mail:
`System.Net.Mail.SmtpClient` (Mail.Mcp server içi).

**Storage**: Postgres — her BC kendi Marten şeması. `merchantDb` (RegisterRequest +
Merchant + SettlementAccount + aktivasyon/challenge bileti + bildirim kaydı),
`commissionDb` (grid — mevcut, sürümleme YOK: 014'e), `identityDb` (EF Core — OpenIddict).
Mail.Mcp + Excel.Mcp KALICILIK TUTMAZ (stateless relay). Mailpit RAM-içi (dev).

**Testing**: Saf domain birim testleri (`tests/Merchant.Api.Tests`,
`tests/Commission.Api.Tests`) — yeni aggregate invariant'ları + challenge/aktivasyon bileti
+ 3-koşul→Active geçişi (idempotent). Handler/HTTP/A2A/LLM entegrasyonu
quickstart ile elle. E2E (Playwright) opsiyonel: Admin "Merchant Talepleri" onay/ret akışı.

**Target Platform**: Linux/dev sunucu; Aspire AppHost (Postgres + RabbitMQ + Identity +
BC API'leri + Admin BFF + Merchant.Agent + Mail.Mcp + Mailpit + simüle aday site).

**Project Type**: Mikroservis / web-service seti (mevcut çok-projeli çözüm; BFF + agent +
MCP servisleri).

**Performance Goals**: Etkileşimli onboarding (throughput hedefi yok). SC-007: son koşul
tamamlanınca ≤1 dk içinde Active. Challenge doğrulama senkron (tek HTTP GET).

**Constraints**: Yalnız TL (para birimi modellenmez). MerchantKey hiçbir kanalda düz metin
taşınmaz (yalnız aktivasyon sayfası, bir kez). Mail gönderim başarısızlığı akışı bozmaz,
sessizce kaybolmaz (FR-019). Fail-closed: koşul eksikken charge yetkisi hiçbir yoldan
verilmez. CP.VPOS tipleri slice sınırını geçmez.

**Scale/Scope**: Öğrenme/dev ortamı; aday site simüle. 6 user story (P1×3, P2×2, P3×1),
20 FR. Kapsam dışı: RBAC (G3), kart vault/charge (G5), DB-per-tenant (G4), ECommerce repo
işleri (E1 — bkz. `ecommerce-side-notes.md`), MerchantKey rotasyonu.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası tekrar bak.*

**I. Bounded Context İzolasyonu** — ✅ (bir sanksiyonlu senkron çağrı ile)
- RegisterRequest + aktivasyon/challenge bileti + bildirim kaydı Merchant BC'de; komisyon
  grid Commission BC'de. Cross-BC yalnız integration event (outbox).
- Active koşulu #2 "komisyon grid hazır" Commission BC'de olur → `MerchantCommissionGridReady`
  **event** ile Merchant BC'ye taşınır (FR-016), doğrudan DB/aggregate erişimi YOK.
- Mail.Mcp + Excel.Mcp + Identity.Server = altyapı, BC değil (mevcut istisna çizgisi). Mail.Mcp
  + Excel.Mcp domain bilmez (generic `send_email` / `generate_spreadsheet`); şablon/içerik/kayıt
  çağıran BC'de.
- ⚠️ **Yeni cross-servis bağı**: Identity.Server aktivasyon sayfası → Merchant.Api "aktivasyon
  bileti kullan" ucu (senkron). Anlık tutarlılık gereği ("key bir kez göster") sanksiyonlu
  senkron çağrı; Complexity Tracking'e işlendi.

**II. Zengin Domain Modeli** — ✅
- RegisterRequest (Pending→Approved/Rejected), Merchant (Provisioning eklenir; 3-koşul→Active
  aggregate metodunda, idempotent), aktivasyon/challenge bileti (tek-kullanım/TTL invariant'ları)
  — hepsi private setter + statik Create + davranış metodu. Komisyon grid mevcut aggregate
  (sürümleme YOK, 014).
- Anemik model YOK. externalRef opak VO/alan.

**III. Vertical Slice + CQRS** — ✅
- `Domains/RegisterRequests/Features/{Commands,Queries}`, Merchant/SettlementAccount mevcut
  desen. Agent'a açık işlemler `Features/Agent/**` + `McpTools/` (Payment deseni). Repository
  yok, Marten `IDocumentSession` + `IMessageBus`.

**IV. Result Pattern** — ✅ `FeatureObjectResultModel<T>` / `ResultDomain`; Code resource
sabiti. Challenge/aktivasyon/mükerrer-başvuru beklenen hataları Result ile.

**V. Merkezi Kimlik ve Açık Yetki** — ✅ **AMENDMENT YAPILDI (v1.3.0 → v1.4.0, MINOR)**
- Kural genişletildi: "verme statü-kapılı — yalnız Active" → "**Provisioning sınırlı demet,
  Active tam demet**" (kademeli, charge fail-closed). Anayasa v1.4.0 (2026-08-08).
- Yeni `mail.send` scope (+ Excel.Mcp için `document.generate` veya `mail.send`) + mail atan
  BC başına Identity client (A kararı). Her MCP çağrısı açık yetki taşır.
- MCP yüzeyleri scope-korumalı: Merchant.Api `/mcp` (`merchant.read`/`write`), Commission.Api
  `/mcp` (`commission.read`). `submit_registration` = Merchant.Agent (kendi token'ı). Komisyon
  Excel orkestrasyonundaki read tool'ları (`get_merchant`, `get_merchant_commission_grid`) +
  Excel.Mcp + Mail.Mcp = **harici LLM/MCP client**, **admin-düzlemi token** ile (merchant_id
  claim'siz — AdminPlaneOnly ruhu; merchant kendi verisi dışına çıkamaz). "Varsayılan açık" uç yok.
- Aktivasyon öncesi token verilMEZ: OpenIddict client'ı yalnız aktivasyon (MerchantProvisioned)
  event'iyle provision edilir; Provisioning statüsü + client-yokluğu birlikte fail-closed.

**VI. Spec-Driven Development** — ✅ Tam akış; bu plan `/speckit-plan` çıktısı. Amendment
gerekçeli işlenir (governance). Amendment plandan SONRA `/speckit-constitution` ile.

**Sonuç**: İlke V amendment'ı + tek sanksiyonlu senkron çağrı dışında ihlal yok.

## Project Structure

### Documentation (this feature)

```text
specs/013-merchant-onboarding/
├── plan.md                  # Bu dosya
├── research.md              # Phase 0 — karar kayıtları
├── data-model.md            # Phase 1 — aggregate/entity/event
├── quickstart.md            # Phase 1 — canlı doğrulama senaryoları
├── ecommerce-side-notes.md  # E1 karşı-uç checklist (kapsam dışı bağımlılık)
├── contracts/               # Phase 1
│   ├── merchant-agent-card.md
│   ├── merchant-mcp-tools.md
│   ├── commission-mcp-tools.md
│   ├── merchant-onboarding-rest.md
│   ├── mail-mcp.md
│   ├── integration-events.md
│   └── merchant-descriptor.md
└── tasks.md                 # /speckit-tasks çıktısı (bu komut ÜRETMEZ)
```

### Source Code (repository root)

```text
src/
├── agents/
│   ├── Payment.Agent/                     # mevcut — şablon
│   └── Merchant.Agent/                     # YENİ — A2A host + LLM router + MCP client
│       ├── Program.cs                      # AddA2AServer + MapA2AJsonRpc + MapWellKnownAgentCard
│       ├── MerchantAgentCard.cs            # A2A skills (013: yalnız başvuru; komisyon 014)
│       ├── McpToolProvider.cs              # Merchant.Api /mcp keşfi (Payment deseni)
│       └── ConstValues.cs                  # router instructions
├── services/
│   ├── Merchant.Api/
│   │   ├── Domains/
│   │   │   ├── RegisterRequests/           # YENİ aggregate + slice
│   │   │   │   ├── RegisterRequest.cs
│   │   │   │   ├── DomainControlChallenge.cs   # tek-kullanım/TTL bilet
│   │   │   │   └── Features/{Commands,Queries,Agent}/
│   │   │   ├── Merchants/                   # GENİŞLER: Provisioning, ReturnUrl, externalRef,
│   │   │   │   │                            #   ActivationTicket, 3-koşul→Active metodu
│   │   │   │   └── Features/{Commands,Queries,Agent}/
│   │   │   └── SettlementAccounts/          # mevcut (Active koşulu #1 kaynağı)
│   │   └── McpTools/MerchantOnboardingMcpTools.cs   # YENİ /mcp — submit_registration + get_merchant (read)
│   └── Commission.Api/
│       ├── Domains/MerchantCommissions/     # mevcut grid + Draft/Ready statü + finalize → GridReady event
│       └── McpTools/MerchantCommissionMcpTools.cs   # YENİ /mcp — get_merchant_commission_grid (read)
├── others/
│   ├── Common/
│   │   └── Mail/                            # YENİ IMailSender + MailMcpClient (yalnız deterministik mailler)
│   ├── Shared/IntegrationEvents.cs          # YENİ event'ler (MerchantProvisioned, ...GridReady)
│   ├── Identity.Server/
│   │   ├── Pages/Activation/                # YENİ Razor Pages (bugün token-only)
│   │   └── (yeni client'lar + mail.send scope seed; MerchantProvisioned consume)
│   ├── Mail.Mcp/                            # YENİ altyapı — generic send_email MCP server
│   │   ├── Program.cs                       # AddMcpServer + MapMcp("/mcp") + RequireAuthorization(mail.send)
│   │   └── SendEmailMcpTool.cs              # tek tool, SMTP config'ten (dev: Mailpit)
│   └── Excel.Mcp/                           # YENİ altyapı — generic generate_spreadsheet (ClosedXML)
│       ├── Program.cs                       # AddMcpServer + MapMcp("/mcp")
│       └── GenerateSpreadsheetMcpTool.cs    # tek tool, satır/sütun → .xlsx
├── ui/Admin/
│   └── Pages/RegisterRequests/              # YENİ "Merchant Talepleri" (listele/onayla/reddet)
└── aspire/AppHost/                          # Merchant.Agent + Mail.Mcp + Excel.Mcp + Mailpit + simüle aday site
```

**Structure Decision**: Mevcut çok-projeli mikroservis düzeni korunur. Yeni projeler mevcut
klasör konvansiyonlarına yerleşir. Mail.Mcp + Excel.Mcp + Identity.Server altyapı (`others/`,
BC değil, versiyonlanmaz). Agent'a/harici LLM'e açık işlemler `Features/Agent/**` + `McpTools/`
(Payment.Api deseni). Komisyon Excel maili **bespoke agent projesi İÇERMEZ** — 013 yalnız
BC MCP yüzeyleri + Excel.Mcp + Mail.Mcp'yi sağlar; süren harici LLM/MCP client seçimi 013 dışı.
Cross-BC yalnız `Shared` event'leri (outbox).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Identity.Server → Merchant.Api senkron HTTP (aktivasyon bileti kullan) | Aktivasyon sayfası Identity'de (FR-009), ama merchant/key/bilet custody Merchant BC'de (İlke I). Sayfa bileti kullanmak için BC'ye senkron sormalı (anlık tutarlılık — key bir kez gösterilir). | (a) Key'i Identity'de saklamak → merchant domain'i altyapıya sızar (İlke I ihlali). (b) Event-only → aktivasyon senkron kullanıcı akışı; async event "bir kez göster"i garanti etmez. Sanksiyonlu senkron çağrı anayasada öngörülü. |
| Provisioning statüsü + kademeli yetki | Spec çekirdeği: insan onayı + out-of-band key + koşullu tam yetki. | Tek Active statüsü → "sınırlı yetkiyle eksik tamamlama" ve fail-closed charge imkânsız. |
| Komisyon Excel maili = harici LLM/MCP orkestrasyon (deterministik pipeline değil) | Kullanıcı tercihi: "her şey MCP/agent". Excel+mail insan-yüzlü bildirim, kritik yol değil. 013 MCP yüzeylerini sağlar; orkestratör client seçimi ertelendi. | (a) BC handler pipeline → agentic MCP tercihine aykırı. (b) Bespoke ops-agent projesi → gereksiz host; MCP yüzeyleri yeterli. **Sınır**: aktivasyon (key linki) + admin bildirim + grid-hazır koşulu deterministik kalır (güvenlik/finansal statü LLM'e verilmez). |