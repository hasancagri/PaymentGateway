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
## Sonuç — Canlı Doğrulama (2026-09-18)

Sistem Aspire'dan tam ayakta; Claude Desktop konektörleri `pg-merchant`/`pg-commission`
(`scripts/claude-desktop-pg-mcp.sh` köprüsü: PRM henüz yok → client_credentials `admin-ui`
token'ı header'la; 15 dk ömür, dolunca Desktop restart). Test merchant'ı: Test Kitabevi
`f7455335-aef0-4eca-a4fd-5a71516157a8`.

- **Senaryo 1 GEÇTİ** — liste/detay Desktop'tan; adres güncelleme + kalıcılık + bilinmeyen-Id
  negatifi (`COMMON_MESSAGE_RECORD_NOT_FOUND`, çökme yok) MCP katmanından curl'le.
  `admin_get_merchant` alan seti: yalnız hassas-dışı 8 alan (email/gsm/iban/kimlik/vergi/key YOK).
- **Senaryo 2 GEÇTİ (ekran formu hariç)** — tool yanıtlarında kişisel veri alanı yok; Desktop
  agent'ı email değişikliğini reddedip ekrana yönlendirdi (link üretemedi — tool description'da
  yol yok, mini-fix TODO); sensitive GET/PUT round-trip curl'le doğrulandı (email değişti,
  `admin_get_merchant`'ta hâlâ yok); `/Merchants/Sensitive` 200. Tarayıcı form akışı elle
  koşulmadı (kullanıcı park etti — merchant self-service kararıyla birlikte ele alınacak).
- **Senaryo 3 GEÇTİ** — oluştur (%2,5+1TL) → görüntüle → marj %3 → ikinci aktif politika
  duplicate reddi → pasife alma, tümü Desktop sohbetinden. Son durum: tiers [0+, %3, 0TL], Passive.
- **Senaryo 4 GEÇTİ** — silinen uçlar 404 (PUT /merchants/{id} 405 `Allow: GET` — path'te yalnız
  GetMerchant kaldı; bare /merchants 400 = versioning artifact'ı, uç yok); kalanlar 200
  (GetMerchant, sensitive çifti, commission-policies/{merchantId}); Admin /Merchants +
  /CommissionPolicies 404, /Merchants/Sensitive 200; `payment-agent`/`merchant-agent` token 401
  (seed + store prune), `admin-ui` 200; build 0 hata + 94 test.
- Not: redeem ucu quickstart öngereksinimlerdeki bahsine rağmen zaten yoktu (023'te söküldü) — adım atlandı.
