# Tasks: Hassas Merchant Verisi — Hosted Link Erişimi

**Feature**: `048-sensitive-hosted-link` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: farklı dosya, tamamlanmamış task'a bağımlı değil → paralel çalışılabilir
- **[USn]**: user story etiketi (yalnız story fazlarında)
- Domain-TDD (İLKE VI): `SensitiveEntrySession` invariant'ları test-first — test ÖNCE, kırmızı görülür, sonra aggregate.

## Path Conventions

Servis düz: `src/services/Merchant.Api/` (csproj BC kökünde). Testler `src/services/Merchant.Api.Tests/`.

---

## Phase 1: Setup (Shared Infrastructure)

- [X] T001 [P] `SensitiveEntryOptions` oluştur (`PublicBaseUrl` [Required] + `LinkLifetime` default 15dk) — `src/services/Merchant.Api/Options/SensitiveEntryOptions.cs`; appsettings.json'a `SensitiveEntryOptions` section (dev default `PublicBaseUrl`)
- [X] T002 [P] Gömülü HTML dosyaları oluştur — `src/services/Merchant.Api/Pages/SensitiveEntry/layout.html`, `view.html`, `success.html`, `notfound.html` (inline CSS + `<meta robots noindex>`; `{{placeholder}}`); `Merchant.Api.csproj`'a `<EmbeddedResource Include="Pages\SensitiveEntry\*.html" />`
- [X] T003 [P] Dar HTML encoder helper (`& < > " '`) — `src/services/Merchant.Api/Utils/SensitiveHtmlEncoder.cs` (ECom 078 deseni; framework `HtmlEncoder.Default` KULLANMA)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Tüm story'ler bu aggregate'e bağlı — önce bu.**

- [X] T004 [P] Domain-TDD testleri yaz (test-first, KIRMIZI olmalı) — `src/services/Merchant.Api.Tests/SensitiveEntrySessionTests.cs`: INV-1 (boş merchantId/userId + ≤0 lifetime RET), INV-2 (Create sonrası usable), INV-3 (süre geçince !usable), INV-4 (Consume tüketir), INV-5 (ikinci Consume Error), INV-6 (süresi geçmiş Consume Error), INV-7 (token her seferinde farklı + base64url charset)
- [X] T005 `SensitiveEntrySession` aggregate — `src/services/Merchant.Api/Domains/Merchants/SensitiveEntrySession.cs`: `: AggregateRoot`; alanlar (Token/MerchantId/RequestedByUserId/ExpiresAt/ConsumedAt); `Create(merchantId, userId, lifetime)`→`ResultDomain<T>`, `Consume(now)`→`ResultDomain`, `IsUsable(now)` saf getter. T004 testlerini YEŞİL yap. Gerekli hata kodları `Constants/MerchantResourceConstants.cs`'e

---

## Phase 3: User Story 1 — Agent link verir, insan görüntüler (P1) 🎯 MVP

**Goal**: Agent hassas veriyi sohbete sızdırmadan link üretir; insan tarayıcıda merchant'ın değerlerini görür.

**Independent Test**: Agent'tan link istenir → yanıtta hiçbir hassas alan yok; link tarayıcıda açılınca değerler görünür.

- [X] T006 [US1] `RequestSensitiveLink` MCP tool + handler — `src/services/Merchant.Api/Domains/Merchants/Features/Agents/Commands/RequestSensitiveLink.cs`: `[McpServerTool] admin_request_sensitive_link` (`merchant.admin` scope); handler `SensitiveEntrySession.Create` → `session.Store` → `{ url = {PublicBaseUrl}/merchants/sensitive/{token}, expiresAt, message }`. Hassas alan YANITA GİRMEZ. Tool description: "veriyi sohbete yazma, yalnız link"
- [X] T007 [US1] `SensitiveEntryEndpointExtension` GET ucu — `src/services/Merchant.Api/SensitiveEntryEndpointExtension.cs`: `GET /merchants/sensitive/{token}` anonim; token lookup → `IsUsable(UtcNow)` ise `bus.InvokeAsync(GetMerchantSensitiveQuery(session.MerchantId))` → `view.html` mevcut değerlerle DOLU (dar-encoder, `value=""` tırnaklı), 200 `text/html`; değilse `notfound.html` 404. GET TÜKETMEZ
- [X] T008 [US1] `Program.cs` bağlama — `MapSensitiveEntryEndpoints()` çağır; `SensitiveEntryOptions` bind (`AddOptions.BindConfiguration.ValidateDataAnnotations.ValidateOnStart` + düz T singleton); gerekirse `opts.Discovery.IncludeType(...)`. `src/services/Merchant.Api/FLOW.md`'ye domain adımı ekle (link üret → aç → görüntüle → tüket, kenar-anchor `SensitiveEntrySession.Create/Consume`)

**Checkpoint**: US1 tek başına test edilebilir (link üret + GET görüntüle).

---

## Phase 4: User Story 2 — İnsan düzenler (P2)

**Goal**: Açık sayfada hassas alan düzenlenip kaydedilir; başarıda link ölür.

**Independent Test**: Geçerli link açılır, alan değiştirilir, kaydedilir; değer kalıcı + aynı link tekrar kullanılamaz.

- [X] T009 [US2] `SensitiveEntryEndpointExtension` POST ucu (aynı dosya, T007 üstüne) — `POST /merchants/sensitive/{token}`: `IsUsable` değilse nötr 404; form → `bus.InvokeAsync(UpdateMerchantSensitiveCommand)`; başarı → `session.Consume(UtcNow)` + `session.Store` → PRG redirect → `success.html`; doğrulama hatası → `view.html` + hata (dar-encoder) 400, link YAŞAR (tüketilmez); merchant yok → nötr 404
- [X] T010 [US2] PRG davranışı + `success.html` bağlama — POST başarı redirect (GET success), yenileme resubmit yapmaz

**Checkpoint**: US1 + US2 birlikte (görüntüle → düzenle → kaydet → link ölür).

---

## Phase 5: User Story 3 — Nötr red (P2)

**Goal**: Bilinmeyen/süresi geçmiş/tüketilmiş link ayırt edilemez nötr 404.

**Independent Test**: Üç token (uydurma + süreli + tüketilmiş) → üçü aynı 404 gövde+durum.

- [X] T011 [US3] Nötr 404 doğrulama + test — GET+POST'ta üç red yolu (bilinmeyen/süreli/tüketilmiş) aynı `notfound.html` + 404 döndüğünü teyit; `src/services/Merchant.Api.Tests/`'e endpoint-seviyesi nötr-red testi (aynı gövde+status)

---

## Phase 6: Polish & Cross-Cutting (Söküm + Doğrulama)

- [X] T012 `src/ui/Admin` projesini komple SİL (klasör + tüm dosyalar)
- [X] T013 Admin proje referansını kaldır — `PaymentGateway.slnx` (Admin satırı) + `src/aspire/AppHost/AppHost.cs` (`AddProject<Projects.Admin>` + `WithReference`) + `src/aspire/AppHost/AppHost.csproj` (ProjectReference)
- [X] T014 BFF sensitive endpoint eşlemelerini kaldır — `src/services/Merchant.Api/Domains/Merchants/MerchantEndpointExtension.cs`'te `GetMerchantSensitiveGroupItemEndpoint` + `UpdateMerchantSensitiveGroupItemEndpoint` çağrıları sökülür; `GetMerchantSensitive`/`UpdateMerchantSensitive` **handler'ları KALIR** (endpoint sarmalayıcıları silinebilir, handler bus için durur)
- [X] T015 Ölü `admin-ui` client seed prune — `src/others/Payment.Identity/Config.cs`'ten `admin-ui` ClientSeed kaldır + `RetiredClientIds`'e ekle (açılışta store'dan prune); ilgili `RequireSecret("admin-ui")` + config secret temizliği
- [X] T016 Denetim izi — link üretiminde `credential/sensitive_link_created` log (merchantId + RequestedByUserId; token DEĞİL) — `RequestSensitiveLink` handler'ında
- [X] T017 Doğrulama (CANLI YOK) — `dotnet build` 0 hata + `dotnet test` tümü yeşil (SensitiveEntrySessionTests + nötr-red testi dahil); Admin söküldükten sonra çözüm temiz derlenir

---

## Dependencies

- **Phase 1 (T001-T003)**: paralel, story'lerden önce
- **Phase 2 (T004→T005)**: T004 test-first (kırmızı) → T005 aggregate (yeşil); tüm story'ler buna bağlı
- **US1 (T006-T008)**: Foundational sonrası. T006 (tool) ‖ T007 (GET) ayrı dosya paralel; T008 ikisine bağlı
- **US2 (T009-T010)**: T007 ile AYNI dosya (SensitiveEntryEndpointExtension) → T007 sonrası, paralel DEĞİL
- **US3 (T011)**: T007 + T009 sonrası (iki uç da nötr-red içermeli)
- **Phase 6**: yeni kanal çalıştıktan sonra. T012→T013 sıralı (silme→referans); T014/T015/T016 [P] farklı dosya; T017 en son

## Parallel Opportunities

- Setup: T001 ‖ T002 ‖ T003
- US1: T006 ‖ T007 (ayrı dosya)
- Polish: T014 ‖ T015 ‖ T016 (ayrı dosya)

## Implementation Strategy

- **MVP = US1** (Phase 1+2+3): agent link + görüntüleme. Tek başına değer + test edilebilir.
- Sonra US2 (düzenleme) → US3 (nötr-red sertleştirme) → Phase 6 (Admin söküm + doğrulama).
- Domain-TDD yalnız aggregate'te (T004-T005); endpoint/tool/HTML test-sonra (İLKE VI: handler/endpoint/UI kuralın dışı).
- CANLI test YOK (kullanıcı kararı) — doğrulama build+test (T017); Aspire canlı senaryo quickstart.md'de ileriye referans.

## Toplam

17 task: Setup 3, Foundational 2, US1 3, US2 2, US3 1, Polish 6.
