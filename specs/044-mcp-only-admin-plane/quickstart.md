# Quickstart: MCP-Only Admin Düzlemi (044) — Canlı Doğrulama

## Önkoşullar

- Docker ayakta (Postgres + RabbitMQ + Mailpit Aspire'dan gelir).
- `dotnet run --project src/aspire/AppHost/AppHost.csproj` — tüm sistem Aspire'dan.
- Claude desktop'ta Merchant.Api + Commission.Api `/mcp` konektörleri bağlı
  (`external-admin-agent`, OAuth PKCE — 042/043 kurulumları).
- En az bir merchant kayıtlı (yoksa Senaryo 0 ile üret).
- 043 quickstart senaryoları (T011/T018/T022/T028) GEÇMİŞ olmalı — 044 sökümü onların
  üstüne kurulur.

## Senaryo 0 — Hazırlık (gerekirse)

`submit_registration` ile başvuru + `admin_approve_registration` ile onay → Active merchant.

## Senaryo 1 — Merchant sohbetten yönetimi (US1)

1. Claude desktop: "merchant listesini göster" → `admin_get_merchants`; yanıtta
   Email/GsmNumber ALANI YOK (sözleşme düzeyinde — boş bile değil).
2. "X merchant'ının detayını göster" → `admin_get_merchant`; hassas/sır alan yok.
3. "X'in adresini 'Yeni Mah. 5' yap" → `admin_update_merchant`; yanıt güncel adresi gösterir.
4. Kalıcılık: `admin_get_merchant` tekrar → adres güncel.
5. Negatif: var olmayan Id ile güncelleme → kayıt-bulunamadı mesajı (çökme yok).

**Beklenen**: hassas-dışı tüm merchant yönetimi ekransız tamamlanır (SC-001, SC-002).

## Senaryo 2 — Hassas veri yalnız ekrandan (US2)

1. `admin_get_pending_registrations` çağır → yanıtta Email/GsmNumber yok.
2. Sohbette "X'in email'ini değiştir" iste → tool bu parametreyi kabul etmez; agent
   hassas-veri sayfası linkini verir.
3. Tarayıcı: `/Merchants/Sensitive?merchantId=<id>` (042 admin login) → Email, GSM, TCKN,
   IBAN, vergi no görünür; Email değiştir + kaydet.
4. Kalıcılık: sayfayı yenile → yeni Email; `admin_get_merchant` → Email alanı HÂLÂ yok.

**Beklenen**: hassas alan uçtan uca yalnız ekrandan (SC-003).

## Senaryo 3 — Komisyon sohbetten (US3)

1. "X'e komisyon tanımla: 0'dan itibaren %2,5 + 1 TL" → `admin_create_commission_policy`.
2. `admin_get_commission_policy` → kademe doğru.
3. "Marjı %3 yap" → `admin_update_commission_margin`; `admin_get_commission_policy` → yeni kademe.
4. "Politikayı pasife al" → `admin_change_commission_status`; negatif: ikinci aktif politika
   oluşturma → duplicate hatası.

**Beklenen**: komisyon tam yaşam döngüsü ekransız (SC-001).

## Senaryo 4 — Söküm doğrulaması (US4; Senaryo 1-3 GEÇMEDEN BAŞLAMA)

1. REST 404 kontrolleri (gateway veya doğrudan servis):
   - `GET/POST/PUT /api/v1/merchants` (liste/create/update/status) → 404
   - `GET /api/v1/register-requests` + approve/reject → 404
   - `POST/PUT/GET /api/v1/commission-policies` (create/margin/status/liste/calculate) → 404
2. KALANLAR çalışır: merchant token'ıyla `GET /api/v1/merchants/{id}` ve
   `GET /api/v1/commission-policies/{merchantId}` 200; aktivasyon redeem akışı bozulmaz.
3. Admin UI: `/Merchants` + `/CommissionPolicies` 404; `/Merchants/Sensitive` çalışır.
4. Identity: `merchant-agent` + `payment-agent` client'ları token ALAMAZ (seed silindi);
   `admin-ui` + `external-admin-agent` token almaya devam eder.
5. `dotnet build` + `dotnet test` → 0 hata, 0 regresyon (SC-005).

**Beklenen**: eski admin yüzeyi erişilemez, makine kanalları kesintisiz (SC-004).