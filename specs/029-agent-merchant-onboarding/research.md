# Research: Agent-Bazlı Merchant Onboarding Dirilişi (029)

**Date**: 2026-08-14 | **Spec**: [spec.md](spec.md)

## R1 — MCP host: Merchant.Api `/mcp` (Merchant.Agent'sız)

**Decision**: MCP yüzeyi doğrudan Merchant.Api'de (`AddMcpServer().WithHttpTransport(Stateless)
.WithToolsFromAssembly()` + `MapMcp("/mcp").RequireAuthorization(MerchantWrite)`). Merchant.Agent
(A2A) bu akışa GİRMEZ.

**Rationale**: Tüketen LLM zaten ECommerce ChatAgent (032) — `DropShopGateway:McpUrl =
http://localhost:5202/mcp` config'i Merchant.Api'nin portunu (5202, launchSettings doğrulandı)
gösteriyor. 016 kuralı korunur: MCP'yi yalnız agent çağırır; araya ikinci agent koymak gereksiz hop.
013'te birebir aynı wiring vardı (commit `8691809^` Program.cs:106-129 doğrulandı) — desen geri gelir.

**Alternatives considered**: Merchant.Agent üzerinden A2A köprüsü — reddedildi (ekstra hop, ölü
Merchant.Agent'ı diriltme maliyeti, LLM zaten karşı tarafta).

## R2 — Tool sözleşmesi: adlar korunur, parametreler 023 setine geçer

**Decision**: Tool adları AYNEN `submit_registration` + `registration_status` (ECommerce
`ConstValues.cs:79-80` allowlist'i değişmez). `submit_registration` parametreleri eski
domain/legalName/taxId/contactEmail/webhookUrl setinden 023 alan setine geçer:
`type, name, email, gsmNumber, address, iban, contactName, contactSurname` + opsiyonel
`identityNumber, taxOffice, taxNumber, legalCompanyTitle`. `registration_status` parametresi
`domain` → `email`.

**Rationale**: ECommerce tarafında tool KEŞFİ dinamik (`ListTools`), imza ECommerce kodunda sabit
değil — yalnız ad allowlist'i + prompt alan enjeksiyonu var. Ad korunursa ECommerce kod değişikliği
config + prompt metnine iner (spec FR-010/FR-011).

**Alternatives considered**: Eski alan setini koruyup gateway'de map'lemek — reddedildi (domain/
webhookUrl artık 023 Merchant'ta yok; uydurma eşleme üretirdi).

## R3 — `ecommerce-onboarding` istemcisi ZATEN seed'de

**Decision**: Identity.Server `Config.cs` istemci listesinde `ecommerce-onboarding`
(merchant.read + merchant.write, secret `Clients:ecommerce-onboarding:Secret` — appsettings dev
değeri `ecommerce-onboarding-dev-secret`) 013 E1'den beri duruyor; ECommerce
`OnboardingGatewayTokenHandler` aynı kimlikle token alıyor. FR-012 = doğrulama, yeni kod değil.

**Rationale**: 022 sökümü Identity seed'ine dokunmamış (canlı doğrulama: Config.cs:70-77).

## R4 — Statü sorgusu anahtarı: e-posta, en-son kayıt, case-insensitive

**Decision**: `registration_status(email)` — normalize edilmiş (Trim) e-posta ile
`CreatedTime`'a göre EN SON RegisterRequest esas alınır; karşılaştırma case-insensitive
(Marten tarafında `ToLower()` karşılaştırması). Approved yanıtı Merchant document'ından
MerchantId + MerchantKey okur (dev-açık karar; spec Assumptions).

**Rationale**: Eski anahtar `domain` 023 alan setinde yok; e-posta hem başvuru kimliği hem mükerrer
kuralının (FR-003) anahtarı — tek anahtar tutarlılığı.

**Alternatives considered**: RequestId ile sorgu — reddedilmedi, YANITTA döner (submit RequestId
verir, statü mesajında görünür) ama sorgu anahtarı e-posta kalır: sohbet oturumu kaybolunca
yönetici RequestId'yi bilmeyebilir, e-postayı bilir.

## R5 — Onay akışı: Approve handler'ı `Merchant.Create` + `MerchantCreated` yayını

**Decision**: `ApproveRegisterRequest` handler'ı: (1) request yükle, (2) `Merchant.Create(...)`
(023 fabrikası — Active doğar, MerchantKey üretir), (3) `session.Store(merchant)`,
(4) `bus.PublishAsync(MerchantCreated(...))` ([Transactional] outbox — CreateMerchant slice'ındaki
yayınla birebir), (5) `request.Approve(merchant.Id)`. Identity `MerchantClientEventHandler`
Active statüde tam demeti (cards.write dahil) zaten açıyor — dokunulmaz.

**Rationale**: 023'ün doğum yolu (CreateMerchant handler'ı) aggregate fabrikası + outbox yayınından
ibaret; approve handler'ı aynı zinciri kendi slice'ında koşturur (015 kuralı: slice'lar birbirini
çağırmaz, bilinçli tekrar). Anayasa V'in 013 "MerchantProvisioned/Provisioning" kademesi 023'te
fiilen `MerchantCreated` + doğrudan Active modeline evrildi (mevcut master davranışı); bu spec o
davranışı DEĞİŞTİRMEZ, aynısını kullanır.

**Alternatives considered**: Approve'un CreateMerchant slice'ını `IMessageBus` ile çağırması —
reddedildi (015: slice→slice çağrı yasak; kod tekrarı bilinçli kabul).

## R6 — Doğrulama tekrarı: RegisterRequest.Submit, Merchant.Create kurallarını AYNEN kopyalar

**Decision**: Tip-uyum matrisi + TR IBAN mod-97 + e-posta regex'i `RegisterRequest.Submit`
fabrikasında inline tekrarlanır (Merchant.cs'ten kopya). Onayda `Merchant.Create` ikinci kez
doğrular (teorik çifte güvence; spec edge case).

**Rationale**: 015 kuralları — aggregate'ler birbirinin metodunu çağırmaz, private helper yok,
kod tekrarı bilinçli. Başvuru anında hata yakalamak (sohbette anında geri bildirim) onay anına
ertelemekten iyi.

## R7 — Admin UI: RegisterRequests ekranı yeniden doğar (bugün silinen desen)

**Decision**: `Pages/RegisterRequests/Index` (liste + satırda Onayla/Reddet formu, red nedeni
input'u) + `RegisterRequestApiClient` + Program.cs DI + nav linki — bu oturumda silinen 013
ekranının 023 alan setine uyarlanmış hâli. Uçlar: `GET /api/v1/register-requests` (liste),
`POST /api/v1/register-requests/{id}/approve`, `POST /api/v1/register-requests/{id}/reject`
(gövde: `{ reason }`); hepsi `merchant.write` + `AdminPlaneOnly`.

**Rationale**: Aynı ihtiyaç, hazır desen (git tarihçesi `8691809^`); Admin BFF kural sızdırmaz,
yalnız API sonucu gösterir.

## R8 — ECommerce değişikliği: yalnız config + prompt

**Decision**: `appsettings.json` `DropShopGateway.Onboarding` bölümü yeni alan setiyle güncellenir
(Type/Name/Email/GsmNumber/Address/Iban/ContactName/ContactSurname + koşullu vergi/TCKN alanları,
dev örnek değerleriyle); `Program.cs:149-156` prompt enjeksiyonu ve `Prompts.
AdminOnboardingInstructions` metni yeni alanları sayar, durum sorgusunun e-posta ile yapıldığını ve
Approved yanıtındaki MerchantId+MerchantKey'in 033 formuna girileceğini söyler. Tool adları,
`McpClients.MachineOnboarding`, token handler, scope — DEĞİŞMEZ.

**Rationale**: Keşif (Explore, 2026-08-14): araçlar ListTools ile dinamik keşfediliyor; sözleşme
adı + prompt dışında ECommerce bağımlılığı yok.

## R9 — Test kapsamı: saf domain birim testleri

**Decision**: `tests/Merchant.Api.Tests`'e `RegisterRequest` testleri eklenir (023 deseni):
Submit doğrulama matrisi (tip-uyum, IBAN, e-posta), Approve/Reject statü makinesi + ikinci karar
reddi. Handler/HTTP/MCP entegrasyonu test edilmez — quickstart senaryolarıyla elle doğrulanır
(bilinçli erteleme, CLAUDE.md).
