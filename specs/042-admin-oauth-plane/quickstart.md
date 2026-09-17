# 042 Admin OAuth — Canlı Doğrulama

1. `dotnet user-secrets set "BootstrapAdmin:Email" "admin@test.local" --project src/others/Identity.Server`
   `dotnet user-secrets set "BootstrapAdmin:Password" "Test123!" --project src/others/Identity.Server`
2. `dotnet run --project src/aspire/AppHost/AppHost.csproj` (tüm sistem).
3. Tarayıcı/`curl` ile PKCE'li `GET /connect/authorize` isteği at (RFC 7636 Appendix B test vektörü):
   `client_id=external-admin-agent`, `redirect_uri=https://claude.ai/api/mcp/auth_callback`,
   `code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM`, `code_challenge_method=S256`.
   Unauthenticated iken `https://localhost:5101/Account/Login`'e 302 yönlenmeli.
4. Bootstrap admin email+parola ile `/Account/Login`'e POST at (antiforgery token + form-encoded
   `Input.Email`/`Input.Password`).
5. Login başarılıysa `/connect/authorize`'a geri yönlenir; aynı cookie jar ile isteği tekrarla —
   şimdi authenticated olduğundan Claude callback'ine `code=` parametresiyle 302 döner (consent
   ekranı GÖRÜNMEZ — beklenen, seed istemci `ConsentType=Implicit`).
6. O `code`'u `POST /connect/token` ile `grant_type=authorization_code` + `code_verifier` (PKCE
   test vektörünün verifier'ı: `dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk`) kullanarak access
   token'a çevir.
7. Token payload'ını (`base64 -d`, ikinci JWT segmenti) aç: `merchant_id` claim'i OLMAMALI, `sub`
   admin kullanıcının GUID'i olmalı, `scope` istenen scope'ları içeren bir JSON array olmalı.
8. `offline_access` scope'u istenirse (`scope=offline_access merchant.read merchant.write`) dönen
   yanıtta `refresh_token` de gelir. O refresh_token'ı `POST /connect/token` ile
   `grant_type=refresh_token` kullanarak yeni bir access token'a çevir — 200 + yeni `access_token`
   dönmeli (refresh_token rotasyonu: yanıt yeni bir refresh_token da içerir).
9. Regresyon: `curl -X POST https://localhost:5101/connect/token -d grant_type=client_credentials
   -d client_id=admin-ui -d client_secret=admin-ui-dev-secret -d "scope=merchant.read merchant.write"`
   HÂLÂ 200 dönmeli (admin OAuth eklentisi client_credentials yolunu bozmamış olmalı).

**Beklenen sonuç**: 6/7 (authorization_code exchange + claim şekli) + 8 (refresh_token) + 9
(client_credentials regresyonu) hepsi geçerse SC-001/SC-002/SC-004 doğrulanmış olur.

**Not (adım 3 scope seçimi):** Task 7'nin canlı denemesinde `scope=offline_access` tek başına
authorize isteğine verilmişti ama code hiç token'a çevrilmemişti (yalnız authorize→code adımı
kanıtlandı). Bu quickstart'ta tam döngü (code→token→refresh) ilk kez tamamlandığı için iki ayrı
authorize turu koşuldu: biri `merchant.read merchant.write` (offline_access'siz, refresh_token
YOK — standart OAuth davranışı) ile SC-002/temel exchange'i kanıtlamak için, biri de
`offline_access merchant.read merchant.write` ile refresh_token akışını kanıtlamak için.
`external-admin-agent`'ın seed'li `Scopes` listesinde `offline_access` AÇIKÇA yok (bkz.
`Config.cs`), ama OpenIddict bu rezerve/protokol scope'unu (RFC 6749 §6) `RequireScopePermissions`
zorlaması yoksa istemci scope-permission demeti dışında da kabul eder — canlı doğrulandı, kod
değişikliği gerekmedi.

## Sonuç

Tüm adımlar 2026-09-17 tarihinde `dotnet run --project src/aspire/AppHost/AppHost.csproj` ile
ayağa kaldırılan tam sistemde canlı çalıştırıldı. Bootstrap admin secret'ları zaten set'liydi
(`dotnet user-secrets list --project src/others/Identity.Server` → `BootstrapAdmin:Email` /
`BootstrapAdmin:Password` mevcuttu, yeniden set etmeye gerek kalmadı).

### Adım 3-5: Authorize → Login → Authorize (code alma)

```
$ curl -vk -c cookiejar.txt -b cookiejar.txt \
  "https://localhost:5101/connect/authorize?response_type=code&client_id=external-admin-agent&redirect_uri=https%3A%2F%2Fclaude.ai%2Fapi%2Fmcp%2Fauth_callback&scope=merchant.read%20merchant.write&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256&state=xyz"
< HTTP/2 302
< location: https://localhost:5101/Account/Login?ReturnUrl=%2Fconnect%2Fauthorize%3F...
```

```
$ curl -sk -c cookiejar.txt -b cookiejar.txt \
  "https://localhost:5101/Account/Login?ReturnUrl=..." -o login_page.html -w "GET /Account/Login -> %{http_code}\n"
GET /Account/Login -> 200
```
(antiforgery token `__RequestVerificationToken` HTML'den çekildi)

```
$ curl -vk -c cookiejar.txt -b cookiejar.txt \
  -X POST "https://localhost:5101/Account/Login?ReturnUrl=..." \
  --data-urlencode "Input.Email=admin@test.local" \
  --data-urlencode "Input.Password=Test123!" \
  --data-urlencode "__RequestVerificationToken=<token>"
< HTTP/2 302
< location: /connect/authorize?response_type=code&client_id=external-admin-agent&redirect_uri=https%3A%2F%2Fclaude.ai%2Fapi%2Fmcp%2Fauth_callback&scope=merchant.read%20merchant.write&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256&state=xyz
```

```
$ curl -vk -c cookiejar.txt -b cookiejar.txt \
  "https://localhost:5101/connect/authorize?response_type=code&client_id=external-admin-agent&redirect_uri=https%3A%2F%2Fclaude.ai%2Fapi%2Fmcp%2Fauth_callback&scope=merchant.read%20merchant.write&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256&state=xyz"
< HTTP/2 302
< location: https://claude.ai/api/mcp/auth_callback?code=Rdmqe9i0kBsbdiZcTohL5x6WGoxHsVYMbZu8ZHABeKQ&state=xyz&iss=https%3A%2F%2Flocalhost%3A5101%2F
```

Consent ekranı hiçbir adımda GÖRÜNMEDİ (Implicit consent, beklenen — SC-001).

### Adım 6-7: Code → Token exchange + JWT decode (merchant_id yok kanıtı — SC-002)

```
$ curl -sk -X POST https://localhost:5101/connect/token \
  -d "grant_type=authorization_code" \
  -d "code=Rdmqe9i0kBsbdiZcTohL5x6WGoxHsVYMbZu8ZHABeKQ" \
  -d "redirect_uri=https://claude.ai/api/mcp/auth_callback" \
  -d "client_id=external-admin-agent" \
  -d "code_verifier=dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk" \
  -w "\nHTTP %{http_code}\n"

{
  "access_token": "eyJhbGciOiJSUzI1NiIs...",
  "token_type": "Bearer",
  "expires_in": 900,
  "scope": "merchant.read merchant.write"
}
HTTP 200
```

Access token JWT payload'ı (base64url-decode, ikinci segment):

```json
{
  "iss": "https://localhost:5101/",
  "exp": 1789664767,
  "iat": 1789663867,
  "aud": "merchant.api",
  "scope": ["merchant.read", "merchant.write"],
  "jti": "8c246620-9201-430c-9b1b-8fd9851a6506",
  "sub": "0bf04e3e-b7a7-4614-94b0-0668ba6bb90e",
  "name": "admin@test.local",
  "email": "admin@test.local",
  "oi_prst": "external-admin-agent",
  "oi_au_id": "615ff4c2-7179-4977-a89a-6fd4298d6362",
  "client_id": "external-admin-agent",
  "oi_tkn_id": "0278999c-36d5-4812-b6b5-101dc7d1be6e"
}
```

Doğrulama: **`merchant_id` claim'i YOK** (SC-002 karşılandı). `sub` = admin ApplicationUser'ın
GUID'i (`0bf04e3e-b7a7-4614-94b0-0668ba6bb90e`) — client_credentials yolundaki `sub=<client_id>`
davranışından FARKLI, beklenen (insan-düzlemi token). `scope` claim'i istenen iki scope'u içeren
JSON array. `name`/`email` claim'leri de admin kullanıcısına ait (`admin@test.local`).

### Adım 8: offline_access + refresh_token akışı

Ayrı bir authorize turu, bu kez `scope=offline_access merchant.read merchant.write` ile (fresh
cookie jar, yeniden login):

```
$ curl -sk -X POST https://localhost:5101/connect/token \
  -d "grant_type=authorization_code" \
  -d "code=9at-PdvYrn-TTBauuYvIv68BmE-bazKeN8V0TixlFd0" \
  -d "redirect_uri=https://claude.ai/api/mcp/auth_callback" \
  -d "client_id=external-admin-agent" \
  -d "code_verifier=dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk" \
  -w "\nHTTP %{http_code}\n"

{
  "access_token": "eyJhbGciOiJSUzI1NiIs...",
  "token_type": "Bearer",
  "expires_in": 900,
  "scope": "offline_access merchant.read merchant.write",
  "refresh_token": "eyJhbGciOiJSU0EtT0FFUCIs..."
}
HTTP 200
```

Refresh_token grant ile yeni access token alma:

```
$ curl -sk -X POST https://localhost:5101/connect/token \
  -d "grant_type=refresh_token" \
  --data-urlencode "refresh_token=<yukarıdaki refresh_token>" \
  -d "client_id=external-admin-agent" \
  -w "\nHTTP %{http_code}\n"

{
  "access_token": "eyJhbGciOiJSUzI1NiIs...",
  "token_type": "Bearer",
  "expires_in": 899,
  "scope": "offline_access merchant.read merchant.write",
  "refresh_token": "eyJhbGciOiJSU0EtT0FFUCIs..." (rotasyonlu yeni refresh_token)
}
HTTP 200
```

Yeni access token'ın decode edilmiş payload'ı:

```json
{
  "iss": "https://localhost:5101/",
  "exp": 1789664825,
  "iat": 1789663925,
  "aud": "merchant.api",
  "scope": ["offline_access", "merchant.read", "merchant.write"],
  "jti": "85a279d5-2331-43cf-a4ef-4695a4ed23ff",
  "sub": "0bf04e3e-b7a7-4614-94b0-0668ba6bb90e",
  "name": "admin@test.local",
  "email": "admin@test.local",
  "oi_prst": "external-admin-agent",
  "oi_au_id": "999a982a-0fee-460d-9675-077877e476d6",
  "client_id": "external-admin-agent",
  "oi_tkn_id": "4ace6c20-6257-4c91-bd47-2ea470fb6fe0"
}
```

Doğrulama: refresh_token grant başarıyla YENİ bir access token üretti (200 + yeni `jti`/`oi_tkn_id`);
`merchant_id` claim'i burada da YOK; `sub` aynı admin kullanıcısı.

**Not (`offline_access` scope-permission bulgusu):** `external-admin-agent`'ın seed'li `Scopes`
listesinde (`Config.cs`) `offline_access` AÇIKÇA tanımlı DEĞİL — yalnız
`["openid","profile","merchant.read","merchant.write","commission.read","commission.write"]` var.
Buna rağmen `offline_access` istenince authorize + token exchange sorunsuz geçti ve refresh_token
üretildi; kod değişikliği GEREKMEDİ. Bu, OpenIddict'in `offline_access`'i RFC 6749 §6 rezerve
scope'u olarak, istemcinin açık scope-permission demetinden BAĞIMSIZ kabul etmesinden kaynaklanıyor
(bu projede `RequireScopePermissions` zorlaması yapılandırılmamış). Regresyon riski yok — davranış
canlı doğrulandı, dokümante edildi.

### Adım 9: admin-ui client_credentials regresyonu (final gate)

```
$ curl -sk -X POST https://localhost:5101/connect/token \
  -d "grant_type=client_credentials" -d "client_id=admin-ui" \
  -d "client_secret=admin-ui-dev-secret" \
  -d "scope=merchant.read merchant.write" -w "\nHTTP %{http_code}\n"

{
  "access_token": "eyJhbGciOiJSUzI1NiIs...",
  "token_type": "Bearer",
  "expires_in": 899
}
HTTP 200
```

**HTTP 200 — regresyon YOK.** Admin OAuth (authorization_code/PKCE + refresh_token) eklentisi
mevcut `client_credentials` m2m yolunu bozmadı.

### Genel değerlendirme

- **SC-001** (tek login, consent yok, token alınır): KARŞILANDI — adım 3-7 canlı kanıtlı.
- **SC-002** (`merchant_id` claim yok, `AdminPlaneOnly` regresyonsuz kabul eder): KARŞILANDI — hem
  ilk exchange hem refresh sonrası token'da `merchant_id` claim'i yok; downstream policy kodu bu
  dilimde değişmedi (kod tabanı teyitli, bkz. spec Assumptions).
- **SC-003** (`BootstrapAdmin` boşken açılış hatasız): Task 1'de zaten canlı doğrulandı, bu turda
  yeniden test edilmedi (secret'lar zaten doluydu — bu tekrar kapsam dışı, önceki task'ın işiydi).
- **SC-004** (loopback + Claude callback ikisi de authorize'ı tamamlar): İKİSİ DE canlı kanıtlı.
  Claude callback (`https://claude.ai/api/mcp/auth_callback`) yukarıdaki adım 3-8'de. Loopback
  (`http://127.0.0.1:<port>/...`) için AppHost ikinci kez ayağa kaldırılıp ayrı bir authorize turu
  koşuldu:

```
$ curl -vk -c cookiejar3.txt -b cookiejar3.txt \
  "https://localhost:5101/connect/authorize?response_type=code&client_id=external-admin-agent&redirect_uri=http%3A%2F%2F127.0.0.1%3A54231%2Fcallback&scope=merchant.read%20merchant.write&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256&state=loop1"
< HTTP/2 302
< location: https://localhost:5101/Account/Login?ReturnUrl=...
```

Login (aynı admin@test.local/Test123!) sonrası `/connect/authorize`'a geri dönüşte:

```
$ curl -vk -c cookiejar3.txt -b cookiejar3.txt \
  "https://localhost:5101/connect/authorize?response_type=code&client_id=external-admin-agent&redirect_uri=http%3A%2F%2F127.0.0.1%3A54231%2Fcallback&scope=merchant.read%20merchant.write&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256&state=loop1"
< HTTP/2 302
< location: http://127.0.0.1:54231/callback?code=JaYL9Kz11Z-AVrDczigom5p_ht6yCAni49k1n5CDBkM&state=loop1&iss=https%3A%2F%2Flocalhost%3A5101%2F
```

Gerçek bir authorization code, gerçek bir loopback redirect_uri'ye (`http://127.0.0.1:54231/callback`,
dinamik/rastgele port) 302 ile teslim edildi. Ek negatif kontrol — izin verilmeyen bir domain
(`https://evil.example.com/callback`) aynı istemciyle denendi:

```
$ curl -vk "https://localhost:5101/connect/authorize?response_type=code&client_id=external-admin-agent&redirect_uri=https%3A%2F%2Fevil.example.com%2Fcallback&scope=merchant.read%20merchant.write&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256&state=evil1"
< HTTP/2 400
```

**400 — reddedildi** (302 ile evil domain'e yönlendirme YOK). Loopback muafiyeti yalnız
`http(s)://localhost|127.0.0.1` için çalışıyor, keyfi domain'lere genişlemiyor — beklenen izolasyon.

Sistem doğrulama sonunda (her iki AppHost turu da) temiz kapatıldı (`AppHost` + `dcp` +
`Identity.Server` süreçleri `kill -9` ile durduruldu, `ps aux` ile teyit edildi).
