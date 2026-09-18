# Data Model: Hosted Onboarding Form + Credential Teslimi (045)

## OnboardingFormSession (YENİ aggregate — merchantDb)

Store-tetikli, süreli + tek başvuruluk hosted form erişimi.

| Alan | Tip | Not |
|---|---|---|
| Id | Guid | doküman kimliği |
| Token | string | 256-bit URL-safe; link `{PublicBaseUrl}/onboarding/form/{token}` |
| Email | string | başvuru kimliği (normalize, store'un verdiği) |
| ExpiresAt | DateTimeOffset | üretim + `FormLinkLifetime` (varsayılan 24 saat) |
| ConsumedAt | DateTimeOffset? | başarılı başvuru anı; dolu ise oturum ölü |

**Davranış (test-first):** `Create(email, lifetime)`; `Consume(now)` → ResultDomain (süre/çift
tüketim RET); `IsUsable(now)` saf getter. Doğrulama hatası oturumu TÜKETMEZ (D3).

## CredentialRevealLink (YENİ aggregate — merchantDb)

Approve/regenerate ürünü; MerchantId+MerchantKey'i BİR KEZ gösteren teslim linki.

| Alan | Tip | Not |
|---|---|---|
| Id | Guid | doküman kimliği |
| Token | string | 256-bit URL-safe; link `{PublicBaseUrl}/onboarding/reveal/{token}` |
| MerchantId | Guid | teslim edilecek merchant |
| ExpiresAt | DateTimeOffset | üretim + `RevealLinkLifetime` (varsayılan 1 saat) |
| ConsumedAt | DateTimeOffset? | ilk gösterim anı; dolu ise link ölü |

**Davranış (test-first):** `Create(merchantId, lifetime)`; `Consume(now)` (gösterim anında —
GET tüketir, store ekranından FARK: burada gösterimin kendisi teslimdir); `IsUsable(now)`;
`Kill()` (regenerate eskiyi öldürür — `ExpiresAt`'i geçmişe çeker ya da ayrı işaret).

## Mevcut tipler (değişiklik)

- **RegisterRequest**: DEĞİŞMEZ (doğulma yolu form POST'una taşınır; `Submit` aynen).
- **Merchant**: DEĞİŞMEZ (key üretimi/tek-emisyon; emisyon noktası reveal sayfası olur).
- **Options.Onboarding**: `ActivationBaseUrl` (ölü) → `PublicBaseUrl` + `FormLinkLifetime` +
  `RevealLinkLifetime`.
