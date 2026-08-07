# Quickstart: OpenIddict Migrasyonu + BC API Yetkilendirmesi (011)

**Date**: 2026-08-07 | **Contract**: [contracts/auth-model.md](contracts/auth-model.md)

## Önkoşullar

- Dev cert güvenilir: `dotnet dev-certs https --trust` (Identity HTTPS 5101 için).
- Sistem AppHost ile ayakta: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
  (identity-server resource'u dahil; Postgres 5433).
- `identityDb` ilk açılışta migrate + seed olur (2 client + 6 scope).

## S1 — Token verme (US1)

```bash
curl -sk https://localhost:5101/connect/token \
  -d "grant_type=client_credentials&client_id=admin-ui&client_secret=<dev-secret>&scope=merchant.read merchant.write"
```

Beklenen: 200 + `access_token`. Token payload'ında (jwt.io / `jq -R 'split(".")[1] | @base64d'`):
`iss=https://localhost:5101`, `sub=admin-ui`, `aud` merchant.api içerir, **`scope` JSON dizisidir** (FR-010).

Negatif: yanlış secret → 400 `invalid_client`; izinsiz scope (`payment-agent` ile `merchant.write`) →
400 `invalid_scope`.

## S2 — Korumasız erişim reddedilir (US2, SC-001)

```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:<merchant-api-port>/api/v1/merchants   # → 401
```

Yanlış scope (payment.read token'ı ile merchants): → 403. Doğru scope'lu token ile → 200.
Üç API'de birer korunan uç için tekrarla (merchants, banks, pos-accounts).

## S3 — Admin ekran turu (US2, SC-002)

Admin BFF üzerinden mevcut akışlar davranış değişmeden çalışır: merchant listele/oluştur,
settlement hesabı ekle, banka + komisyon grid'i düzenle, bin-card listesi/import. Ekranlarda
401/403 GÖRÜLMEZ (token handler şeffaf).

## S4 — A2A taksit akışı (US3, SC-003)

007/024 akışı uçtan uca: A2A üzerinden taksit sorgusu → seçenek listesi → seçim. Payment.Agent'ın
MCP çağrıları Bearer taşır (payment-api log'unda doğrulanabilir). Agent token edinemezse akış
anlaşılır hata verir (secret'ı geçici boz → sessiz başarı olmadığını gör).

## S5 — Temizlik kanıtı (SC-004, SC-005)

```bash
grep -i duende Directory.Packages.props        # → boş (0 satır)
grep -rn "ApiKey" src/others/Identity.Server    # → boş; /apikeys uçları 404
curl -sk https://localhost:5101/Account/Login -o /dev/null -w "%{http_code}"   # → 404 (login UI yok)
```

## S6 — Çoklu-scope regresyonu (FR-010; 029 tuzağı)

`admin-ui` ile 6 scope'un TAMAMINI iste; dönen token'la üç API'den birer uç çağır → hepsi 200.
(Tek-string scope hatasında bu senaryo 403'lerle çöker — 029'daki canlı bulgunun testi.)