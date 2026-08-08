# Quickstart — Merchant Onboarding (013) Canlı Doğrulama

Feature tamamlandıktan sonra elle koşulacak uçtan-uca senaryolar. Tüm sistem **AppHost**'tan
kalkar (Postgres + RabbitMQ + Identity.Server + Merchant.Api + Commission.Api + Admin BFF +
Merchant.Agent + Mail.Mcp + Excel.Mcp + Mailpit + simüle aday site).

## Ön koşullar

```bash
dotnet build
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

- Mailpit web UI: `http://localhost:8025` (tüm giden mail burada; gerçek adres gerekmez).
- Simüle aday site: descriptor + challenge dosyalarını sunar (AppHost resource).
- Identity.Server: `https://localhost:5101` (dev cert).
- Referanslar: `contracts/` (sözleşmeler), `data-model.md` (varlıklar), `research.md` (kararlar).

---

## S1 — Başvuru + sahiplik kanıtı (US1, FR-001..005)

1. Simüle aday site `shop.test.local` descriptor'ı sunar (zorunlu alanlar dolu).
2. Merchant.Agent'a A2A ile başvur ("shop.test.local ile kayıt olmak istiyorum") → agent
   `submit_registration(domain)` çağırır.
3. Challenge token verilir; simüle site `/.well-known/merchant-challenge/{token}` yayınlar;
   gateway senkron doğrular.
4. **Beklenen**: `RegisterRequest(Pending)` oluşur; Mailpit'te admin adresine "yeni başvuru"
   maili görünür. Merchant kaydı OLUŞMAZ (GetMerchant/token denemesi sonuçsuz — SC-004).

**Negatif**:
- Challenge yayınlamadan dene → talep OLUŞMAZ, admin kuyruğu boş (FR-003).
- Descriptor'da eksik alan → anlaşılır hata, talep yok (FR-002).
- Süresi geçmiş token → reddedilir (Edge).
- Aynı domain ikinci başvuru (Pending varken) → mükerrer talep yok (FR-020).

---

## S2 — Admin onay → merchant doğar + aktivasyon maili (US2, FR-006..008)

1. Admin BFF "Merchant Talepleri" sayfasında Pending talebi aç (domain + descriptor özeti +
   challenge durumu).
2. **Onayla**.
3. **Beklenen**: Merchant kaydı oluşur (statü **Provisioning**, MerchantKey üretildi ama HİÇBİR
   yerde görünmüyor); descriptor `contactEmail`'ine aktivasyon linkli mail Mailpit'te görünür;
   link Identity aktivasyon sayfasına işaret eder.

**Ret yolu**: başka talebi **Reddet** → Rejected; merchant OLUŞMAZ; o domainden yeni başvuru
normal akışla tekrar yapılabilir (FR-008).

**Komisyon ön koşulu değil**: komisyon tanımsızken onay engellenMEZ (FR-007).

---

## S3 — Aktivasyon: key tek-seferlik teslim + Provisioning token (US3, FR-009..010)

1. Maildeki aktivasyon linkini aç → Identity aktivasyon sayfası (`/activation?token=...`).
2. Formu gönder.
3. **Beklenen**: MerchantKey **bir kez** gösterilir ("bir daha gösterilmez" uyarısı); merchant
   **Provisioning**'e geçer; Identity'de OpenIddict client provision edilir.
4. Aynı linki tekrar aç → **reddedilir**, key yeniden gösterilMEZ (FR-009, SC-005).
5. Provisioning token al (`client_id=merchantId`, `client_secret=MerchantKey`, `connect/token`):
   token `merchant.read/write` taşır, **charge yetkisi taşımaz** (FR-010).

**Negatif**:
- Aktivasyondan ÖNCE token dene (client yok) → reddedilir (D1 fail-closed, SC-004/SC-006).
- Süresi dolmuş aktivasyon linki → reddedilir; admin yeni bilet tetikler (Edge).

---

## S4 — Komisyon: gateway-otoriter grid + agentik Excel mail (US4 → B kararı, FR-011)

> 013: komisyon **gateway'in dediğidir**; merchant pazarlık/kabul/ret YAPMAZ. Grid satırları
> bankanın desteklediği tüm taksit sayılarını kapsar.

**Deterministik koşul (otomatik — 013'te tam çalışır):**
1. Admin, merchant için komisyon grid'ini doldur (Admin UI, mevcut BulkUpsert) → grid **Draft**.
2. Draft'ken `get_merchant_commission_grid` → "hazır değil (Draft)"; event YOK, Excel YOK.
3. Admin grid'i **finalize** et (bütünlük: IsMissing yok, BelowBankCeiling yok) → grid **Ready**.
4. **Beklenen (koşul #2)**: `MerchantCommissionGridReady` event'i Merchant.Api'ye ulaşır;
   "komisyon grid hazır" koşulu set olur (log: "Successfully processed", "No known handler" YOK).
   Bu adım komisyon maili atılmasa da gerçekleşir.

**Agentik Excel maili (MCP yüzeyleri hazır; orkestratör client ertelendi — D14):**
5. 013 şunları sağlar: Merchant.Api `get_merchant`, Commission.Api `get_merchant_commission_grid`,
   Excel.Mcp `generate_spreadsheet`, Mail.Mcp `send_email`. Bir **harici LLM/MCP client** (araç
   seçimi 013 dışı) bunları sırayla çağırarak Ready grid'i Excel'e döküp mail atabilir:
   `get_merchant` → `get_merchant_commission_grid` → `generate_spreadsheet` → `send_email`.
6. **Tool bazında doğrulama (client'sız)**: her MCP tool'unu tek tek çağır (token'lı curl / MCP
   inspector) → Ready `get_merchant_commission_grid` tüm taksit satırlarını döner;
   `generate_spreadsheet` geçerli `.xlsx` üretir; `send_email` ile mail Mailpit'te (`:8025`)
   ekli görünür.
7. Draft/hazır-değil grid'de `get_merchant_commission_grid` → `status:"Draft", isEmpty:true`
   (Excel üretilmez).

**Not**: uçtan-uca LLM orkestrasyonu + gelen-mail okuma + kabul/ret + ML-intent pazarlık =
**014** (kapsam dışı). 013 yalnız MCP yüzeylerini + deterministik koşul event'ini garanti eder.

---

## S5 — 3 koşul → otomatik Active (US5, FR-016..017)

Provisioning merchant kendi token'ıyla:
1. Settlement hesabı ekle (`merchants/{id}/settlement-accounts`) → koşul #1.
2. Komisyon grid'i hazır (S4) → koşul #2.
3. ReturnUrl set et (`PUT merchants/{id}/return-url`, HTTPS) → koşul #3.
4. **Beklenen**: üçüncü koşul tamamlanır tamamlanmaz (≤1 dk, SC-007) merchant **insan
   müdahalesi olmadan Active**; yeni token tam yetki demeti taşır.

**Negatif**:
- İki koşulla Active OLMAZ.
- HTTP (HTTPS değil) ReturnUrl reddedilir.
- Sıra fark etmez; hangi koşul son tamamlanırsa geçiş o an tetiklenir (idempotent).

**Admin geçişi**: Active merchant'ı admin Suspended/Passive yap → token verilmez (mevcut
statü-kapılı davranış, DEĞİŞMEZ, FR-017).

---

## S6 — externalRef opak geri-dönüş (US6, FR-018)

1. Merchant'a dönük bir kayıt ucuna `externalRef` ile istek gönder.
2. Kaydı sorgula → `externalRef` **aynen** geri döner.
3. `externalRef` olmadan istek → normal çalışır (alan opsiyonel).

---

## Dual-write / tutarlılık doğrulaması (research D13)

- Commission.Api log: grid yazımı + `MerchantCommissionGridReady` aynı transaction (outbox).
- Merchant.Api consumer log: "Successfully processed message" (tekil `...Handler`), Active
  geçişi idempotent (event iki kez gelirse ikinci no-op).
- Identity log: MerchantProvisioned → client kur; MerchantStatusChanged(Active) → tam scope.

## Kapsam dışı (hatırlatma)

RBAC (G3), kart vault/charge (G5), DB-per-tenant (G4), gelen-mail/ML-intent komisyon pazarlığı
(014), gerçek ECommerce descriptor/challenge (E1 — simüle). Bkz. `plan.md` Scope + `research.md`.