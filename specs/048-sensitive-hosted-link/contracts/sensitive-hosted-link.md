# Contracts: Hassas Hosted Link

## 1. MCP Tool — `admin_request_sensitive_link`

- **Yüzey**: Merchant `/mcp` (Platform token downstream; `merchant.admin` scope, Wolverine scope middleware).
- **Slice**: `Domains/Merchants/Features/Agents/Commands/RequestSensitiveLink.cs` (kendi handler'ı; MCP yalnız agent slice çağırır).
- **Girdi**: `{ merchantId: Guid }`
- **Çıktı**: `{ url: string, expiresAt: DateTimeOffset, message: string }`
  - `url` = `{PublicBaseUrl}/merchants/sensitive/{token}`
  - `message` = "Linki tarayıcıda açın; hassas veriler orada görünür/düzenlenir. Link 15 dk geçerli ve tek kullanımlıktır."
- **Kural**: Hassas alan (email/iban/tckn...) yanıtta veya sohbette ASLA yer almaz. Tool description: "veriyi sohbete yazma, yalnız linki düşür."
- **Hata**: yetkisiz → scope middleware reddeder; merchant yok → `NotFound`.

## 2. HTTP Endpoint — hosted sayfa (anonim, token=capability)

`SensitiveEntryEndpointExtension.MapSensitiveEntryEndpoints` (Merchant.Api kökü, ECom `CredentialEntryEndpointExtension` emsali).

### GET `/merchants/sensitive/{token}`

- Token lookup → `IsUsable(UtcNow)` ?
  - **Evet**: `GetMerchantSensitive(session.MerchantId)` (bus) → gömülü `.html` form, mevcut değerlerle DOLU (dar-encoder + tırnaklı attribute), `200`, `text/html`, `noindex`. **Tüketmez.**
  - **Hayır / token yok / merchant yok**: nötr `notfound.html`, `404`.

### POST `/merchants/sensitive/{token}`

- Token lookup → `IsUsable` ? değilse nötr `404`.
- Form gövdesi → `UpdateMerchantSensitive` (bus, doğrulama dahil).
  - **Başarı**: `session.Consume(UtcNow)` + store → PRG (redirect) → success sayfası. Link ölür.
  - **Doğrulama hatası**: form sayfası + hata (dar-encoder), `400`. **Link YAŞAR** (tüketilmez).
  - **Merchant yok**: nötr `404`.

**Nötr 404 kuralı**: bilinmeyen / süresi geçmiş / tüketilmiş — ayırt edilemez aynı gövde + durum.

## 3. Gömülü HTML sözleşmesi

`Pages/SensitiveEntry/*.html` (EmbeddedResource); `{{placeholder}}` doldurulur, değerler dar-encoder'dan.

| Dosya | Placeholder | Not |
|---|---|---|
| `layout.html` | `{{title}}`, `{{body}}` | dış kabuk + inline CSS + `<meta robots noindex>`; `{{body}}` RAW |
| `view.html` | `{{token}}`, `{{email}}`, `{{gsm}}`, `{{identity}}`, `{{iban}}`, `{{tax}}`, `{{name}}`, `{{error}}` | form, mevcut değerlerle dolu (input `value=""` tırnaklı) |
| `success.html` | — | "kaydedildi, link geçersiz" |
| `notfound.html` | — | bağımsız nötr sayfa |

- Değer enjeksiyonu: her merchant alanı `Encode(& < > " ')` sonra `value="{{...}}"` (tırnaklı). `{{body}}` layout'a RAW.

## 4. Config — `SensitiveEntryOptions`

- `PublicBaseUrl` (string, Required) — Merchant.Api'nin tarayıcıdan erişilir tabanı.
- `LinkLifetime` (TimeSpan, default 15dk).
- Bağlama: `AddOptions<T>().BindConfiguration(nameof(T)).ValidateDataAnnotations().ValidateOnStart()`; düz `T` enjekte.

## 5. Söküm sözleşmesi

- `src/ui/Admin` proje + slnx + AppHost `AddProject<Projects.Admin>` + `WithReference` → kaldırılır.
- `MerchantEndpointExtension`: `GetMerchantSensitiveGroupItemEndpoint` + `UpdateMerchantSensitiveGroupItemEndpoint` çağrıları kaldırılır (metotlar/handler'lar kalır ya da endpoint sarmalayıcı silinir; handler'lar bus için durur).
- Payment.Identity `Config.cs`: `admin-ui` seed kaldırılır + `RetiredClientIds`'e eklenir (açılışta store'dan prune).
