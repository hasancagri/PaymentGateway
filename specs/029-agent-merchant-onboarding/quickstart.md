# Quickstart: Agent-Bazlı Merchant Onboarding (029)

Canlı doğrulama rehberi — [contracts/](contracts/) sözleşmelerini uçtan uca kanıtlar.

## Ön koşullar

1. **PaymentGateway**: `dotnet run --project src/aspire/AppHost/AppHost.csproj` (Postgres 5433 +
   RabbitMQ + Identity 5101 + Merchant.Api 5202 + Admin 5204 + Mailpit).
2. **ECommerce** (S4-S5 için): kendi AppHost'u; ChatAgent `DropShopGateway` config'i dolu
   (McpUrl `http://localhost:5202/mcp`, IdentityAddress `https://localhost:5101`).
3. Token almak için (S1-S3 curl):
   ```bash
   TOKEN=$(curl -sk https://localhost:5101/connect/token \
     -d "grant_type=client_credentials&client_id=ecommerce-onboarding&client_secret=ecommerce-onboarding-dev-secret&scope=merchant.read merchant.write" \
     | jq -r .access_token)
   ```
   MCP çağrıları için MCP Inspector veya `curl` ile JSON-RPC (`tools/call`) kullanılabilir.

## S1 — Başvuru (submit_registration) → Pending

`submit_registration`'ı geçerli 023 alanlarıyla çağır (bkz. [contracts/mcp-tools.md](contracts/mcp-tools.md)).

**Beklenen**: `{ requestId, status: "Pending" }`; Admin `Merchant Talepleri` listesinde satır.

Negatifler: (a) `type=Personal` + `identityNumber` boş → tip-uyum hatası, kayıt yok;
(b) bozuk IBAN → alan hatası; (c) aynı e-posta ile ikinci submit → `RECORD_DUPLICATE`.

## S2 — Admin liste + Onay

Admin UI `http://localhost:5204` → Merchant Talepleri → satırda **Onayla**.

**Beklenen**: başvuru Approved + merchantId bağlı; Merchants listesinde yeni merchant **Active**;
Merchant Detay'da MerchantId + MerchantKey görünür. Merchant.Api logunda `MerchantCreated` yayını,
Identity.Server logunda "Successfully processed message" (çoğul-Handler tuzağı yok — "No known
handler" GÖRÜLMEMELİ).

## S3 — Durum sorgusu (registration_status) → kimlik teslimi

`registration_status(email)` çağır.

**Beklenen**: `Approved` + `merchantId` + `merchantKey` (mk_...). Doğrulama: bu ikiliyle
`connect/token`'dan (client_id=merchantId, client_secret=merchantKey) token alınabiliyor
(Active → tam demet).

Negatifler: bilinmeyen e-posta → `RECORD_NOT_FOUND`; büyük harfli e-posta → aynı kayıt bulunur.

## S4 — Red akışı

Yeni e-postayla S1 tekrar → Admin'de nedenle **Reddet** → `registration_status` yanıtı
`Rejected` + neden. Aynı e-postayla yeniden submit → KABUL (yeni Pending).

## S5 — Uçtan uca sohbet (ECommerce)

ECommerce WebApp → Admin → Onboarding sohbeti: "gateway'e kayıt olmak istiyorum" → asistan alanları
metinle toplar → başvuru gider (S1 arkada). Gateway Admin'den onay (S2) → sohbete "başvurum ne
durumda?" → MerchantId + MerchantKey döner → 033 formuna gir → kaydet.

**Beklenen**: sohbet ekranı dışında hiçbir dosya/DB müdahalesi olmadan zincir tamamlanır (SC-003).

## Kapanış kontrolü

- `dotnet build` 0 hata; `dotnet test tests/Merchant.Api.Tests` yeşil (RegisterRequest testleri dahil).
- Wolverine canlı doğrulama: consumer logunda "Successfully processed message" var.
