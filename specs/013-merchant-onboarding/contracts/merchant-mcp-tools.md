# Contract — Merchant.Api iç MCP tool yüzeyi (`/mcp`)

Yeni yüzey (Payment.Api `/mcp` deseni). `AddMcpServer().WithHttpTransport(Stateless=true)
.WithToolsFromAssembly()` + `MapMcp("/mcp").RequireAuthorization("merchant.write")`. Her tool
`[McpServerToolType]`, yalnız `IMessageBus.InvokeAsync` ile slice sarar (Commands/Queries
değil, `Features/Agent/**`). **Dışa kapalı**: yalnız gateway Merchant.Agent istemcisi tüketir.

## Tool: `submit_registration`

Merchant adayı başvurusu (US1). Descriptor çek + challenge doğrula + RegisterRequest.

- **Girdi**: `{ "domain": "shop.example.com" }`
- **İç akış** (slice): descriptor GET → zorunlu alan doğrula → challenge `Issue(domain)` →
  (aday yayınlar) → senkron `Verify` → geçerse `RegisterRequest.Create(...)` + admin mail
  (IMailSender) → outbox.
- **Sonuç (Result)**:
  - Başarı (talep oluştu): `{ status: "Pending", requestId, message }`
  - Challenge gerekli/beklenen değer: `{ status: "ChallengeRequired", token, expectedValue,
    publishPath: "/.well-known/merchant-challenge/{token}" }`
  - Hata: descriptor erişilemez / alan eksik / mükerrer → `Result` hata (`Code` resource sabiti).
- **İki-adım not**: challenge yayınından önce doğrulama geçmez. Tool ya (a) tek çağrıda
  descriptor+challenge yayınını bekler (simüle host'ta önceden konur), ya da (b) önce
  `ChallengeRequired` döndürüp adayın yayınından sonra ikinci çağrıda doğrular. Quickstart
  simülasyonu (a)'yı kullanır (challenge dosyası doğrulamadan önce yerleştirilir).

## Tool: `registration_status` (opsiyonel, read-only)

- **Girdi**: `{ "domain": "shop.example.com" }`
- **Sonuç**: `{ status: "Pending" | "Approved" | "Rejected", requestId }`

## Tool: `get_merchant` (read-only, harici LLM için)

Komisyon Excel orkestrasyonunun ilk adımı (D14) — merchant iletişim/kimlik bilgisi.

- **Girdi**: `{ "domain": "shop.example.com" }` (veya `{ "merchantId": "..." }`)
- **Sonuç**: `{ merchantId, name, contactEmail, status }` (mevcut GetMerchant/GetMerchantByKey
  slice'ını sarar).
- **Tüketen**: harici LLM/MCP client (client seçimi 013 dışı), **admin-düzlemi token**
  (merchant_id claim'siz). Policy `merchant.read`.

## KAPSAM DIŞI (013 → 014)

- `accept_commission_terms`, `reject_commission_terms` — komisyon pazarlığı 014'e (B kararı).
  013'te bu tool'lar YOK.

## Auth

- `submit_registration` / `registration_status` → `merchant.write` / `merchant.read`; çağıran
  Merchant.Agent (kendi client_credentials'ı, merchant_id claim'siz).
- `get_merchant` → `merchant.read`; çağıran harici LLM/MCP client, admin-düzlemi token.
- Merchant'ın **kendi** (merchant_id claim'li) token'ı bu yüzeye girmez — iç/admin yüzeyi.