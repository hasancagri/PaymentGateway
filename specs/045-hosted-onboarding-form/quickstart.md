# Quickstart: Hosted Onboarding (045) — Canlı Doğrulama

Ön koşul: PG Aspire ayakta (`dotnet run --project src/aspire/AppHost/AppHost.csproj`).
Store-E2E senaryoları için ECom Aspire da ayakta (store quickstart:
`ECommerceWithAgentFramework/specs/078-hosted-onboarding-form/quickstart.md`).
Token: `curl -sk https://localhost:5101/connect/token -d "grant_type=client_credentials&client_id=ecommerce-onboarding&client_secret=<secret>&scope=merchant.read merchant.write"`

## P1 — Form oturumu + hosted form (US1)

1. `POST http://localhost:5202/api/v1/onboarding/sessions` gövde `{"email":"merchant@test.dev"}`
   (Bearer) → `{formUrl, expiresAt, applicationStatus:"None"}`.
2. `formUrl`'i tarayıcıda aç, formu doldur (Personal + TCKN sandbox verisi), gönder → teşekkür
   sayfası; PG Admin bildirim maili Mailpit'te.
3. Aynı çağrıyı tekrarla → `formUrl: null` + `applicationStatus: "Pending"`.
4. Negatif: aynı form linkini ikinci kez aç/gönder → nötr "geçersiz link"; bozuk token → aynı.

## P2 — Approve → mail + tek gösterim (US2)

1. Claude Desktop `/mcp` (merchant.admin): `admin_list_register_requests` → requestId;
   `admin_approve_registration`.
2. Mailpit: başvuru e-postasına teslim maili düştü; linki aç → MerchantId + MerchantKey BİR KEZ
   görünür.
3. Sayfayı yenile / linki tekrar aç → bilgi yok, "PG Admin'e başvurun" sayfası.
4. `admin_resend_credential_link` çağır → yeni mail; ESKİ link ölü, yenisi bir kez gösterir.

## P3 — Durum + validate (US3)

1. `GET /api/v1/onboarding/applications/merchant@test.dev` → `{status:"Approved", message}` —
   MerchantId/MerchantKey alanı YOK. Bilinmeyen e-posta → `status:"None"` (200).
2. `POST /api/v1/onboarding/credentials/validate` doğru ikili → `{valid:true}`; yanlış key →
   `{valid:false}`; Passive merchant → `{valid:false}`.

## P4 — Store E2E (078 kabulü)

Store quickstart S1-S3 + S5 iki sistem birlikte koşarak: PII'siz başlatma, teslim, store
ekranından giriş + anlık doğrulama, PG-kapalı dostane hata.

## P5 — Söküm sonrası (US4, P1-P4 PASS sonrası)

`/mcp` tool listesinde `submit_registration`/`registration_status` yok; S2S uçlar çalışıyor;
form-POST admin bildirim maili sürüyor.
