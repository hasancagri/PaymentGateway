# Research: Hosted Onboarding Form + Credential Teslimi (045)

Kontrat kararları ECom 078'de verildi; buradakiler PG-içi yerleşim kararlarıdır.

## D1 — Hosted sayfalar: Merchant.Api içinde gömülü HTML

- **Decision**: Form sayfası (`GET/POST /onboarding/form/{token}`), teslim sayfası
  (`GET /onboarding/reveal/{token}`) ve sonuç/nötr sayfalar Merchant.Api'de Minimal API +
  gömülü HTML string (Payment.Api 041 `BuildReturnPage` emsali). Razor/SPA kurulmaz.
- **Link tabanı**: `Options.Onboarding.PublicBaseUrl` (dev: `http://localhost:5202`). Ölü
  `ActivationBaseUrl` alanı (kullanımsız kalmış) bu alanla DEĞİŞTİRİLİR. HttpContext base
  kullanılmaz (store 078 D1 gerekçesi).
- **Alternatives**: Admin BFF'e sayfa koymak (yanlış yüzey — sayfalar admin değil,
  başvuran/merchant yüzü); Razor Pages (tek-form için ağır).

## D2 — Token mekanizması: Marten dokümanı, imza değil (store 078 D2 ikizi)

- **Decision**: `OnboardingFormSession` (~24 saat) + `CredentialRevealLink` (~1 saat) ayrı Marten
  dokümanları; 256-bit `RandomNumberGenerator` base64url token, `ExpiresAt` + `ConsumedAt`.
  Süreler Options'tan (`FormLinkLifetime`, `RevealLinkLifetime`).
- **Rationale**: Tek kullanım/geri çekme sunucu-durumu ister; regenerate eski linkleri
  öldürebilmeli (imzalı token geri çekilemez).

## D3 — Form POST doğrulaması: aggregate'te, formda değil

- **Decision**: Form POST'u `RegisterRequest.Submit(...)` fabrikasını aynen çağırır (tip-uyum
  matrisi, IBAN mod-97, e-posta); alan hatalarında form sayfası hatalarla yeniden gösterilir,
  oturum TÜKETİLMEZ. Başarıda oturum tüketilir + PG Admin bildirim maili (mevcut
  `SubmitRegistration` davranışı buraya taşınır).
- **Rationale**: İlke II — kural aggregate'te tek yerde; form yalnız transport.

## D4 — Approve kancası + regenerate

- **Decision**: `AdminApproveRegistration` handler'ına eklenir: `CredentialRevealLink.Create` +
  `SendEmailRequested` publish (aynı `[Transactional]` outbox — commit'siz mail yok). Yeniden
  teslim ayrı MCP tool'u `admin_resend_credential_link` (merchant.admin): Approved merchant'ın
  yaşayan linklerini öldürür, yenisini üretir, mail atar; yanıtta key/link YOK.
- **Alternatives**: MerchantCreated event tüketicisi ile ayrı handler (aynı BC içinde gereksiz
  dolaylılık; kanca zaten tek noktada).

## D5 — Validate ucu semantiği

- **Decision**: `{merchantId, merchantKey}` → merchant yükle, `MerchantKey` birebir eşleşme +
  `Status == Active` → `{valid}`. İkili ve sonuç loglanmaz; yanıtta başka alan yok.
- **Rationale**: Kontrat #3; key yüksek entropili ("mk_"+Guid) — timing kanalı pratik risk değil.

## D6 — Slice yerleşimi (VSA)

- **Decision**: `Domains/OnboardingFormSessions/` (aggregate + endpoint extension: S2S POST
  sessions + form GET/POST) ; `Domains/CredentialRevealLinks/` (aggregate + reveal GET +
  `Features/Agents/Commands/AdminResendCredentialLink`); durum sorgusu
  `Domains/RegisterRequests/Features/Queries/GetOnboardingApplicationStatus`; validate
  `Domains/Merchants/Features/Queries/ValidateMerchantCredentials`. Sayfa GET/POST'ları
  kullanıcı-niyetli (başvuran/merchant tetikler) → Features/Commands.
- **Rationale**: Her aggregate kendi klasörü (conventions); okunabilirlik "bu uç kimin".

## D7 — Mail içeriği

- **Decision**: `SendEmailRequested(to: başvuru e-postası, IsHtml: true)`; gövde kısa Türkçe
  HTML: teslim linki + "bir kez gösterilir + store ekranına girin" yönergesi. Mevcut kontrat/
  worker değişmez.

## D8 — Eski MCP yüzeyi sökümü sıralaması

- **Decision**: `submit_registration` + `registration_status` (029) ancak store 078 quickstart
  S1-S3 canlı PASS sonrası sökülür (045 US4). Admin bildirim maili söküm ÖNCESİ form POST'una
  taşınmış olur (D3) — davranış kaybolmaz.
