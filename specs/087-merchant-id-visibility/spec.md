# Feature Specification: MerchantId Görünürlük Politikası (PG bacağı)

**Feature Branch**: `087-merchant-id-visibility`

**Created**: 2026-10-05

**Status**: Draft

**Input**: Teknik borç S4. İki temel hedef: **(A) merchant sürecinde hiçbir HTML ekran sır/PII render etmez**, **(B) MCP üzerinden hassas veri (credential + finansal/PII) gelmez**. Bugün ihlal: onay sonrası **reveal sayfası** MerchantId+MerchantKey'i HTML'de gösteriyor; reissue de aynı reveal yoluyla teslim ediyor. Karar (kullanıcı, 2026-10-04): credential insana hiç render edilmez; store'a yalnız makine-handoff (HMAC-callback) ile gider; MCP yalnız sır-olmayan veri döner. Dayanak ADR: `adr-mcp-control-plane-no-secret-return`. Bu spec yalnız **PG (Merchant.Api)** bacağını kapsar; store bacağı (ECommerceAgent) bitti (callback alıcısı + gRPC kullanım hazır).

## Clarifications

### Session 2026-10-05

- Q: 045 hosted onboarding formu (OnboardingFormSessions + `/onboarding/{token}` + form HTML) ne olsun? → A: **Tam sök** — store artık formu açmıyor (S2S kayda geçti), form öksüz; tek tüketici ECommerce.
- Q: Başvuru reddedilirse store nasıl öğrensin? → A: **Credential yalnız onayda** aktarılır (onaysız sır yok); red'de callback YOK. Store reddi mevcut `GET /onboarding/status?email=` ile öğrenir.
- Q: MerchantScoped `GetMerchant` MerchantKey döndürüyor — 087'de? → A: **Scrub** — MerchantKey hiçbir query dönüşünde (direk B).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Merchant sürecinde HTML ekran sır göstermez (Priority: P1)

Merchant onboarding ve key-yenileme sürecinde MerchantKey (ve credential çifti) hiçbir HTML ekranda/müşteri sohbetinde görünmez. Credential store'a yalnız makine-handoff ile gider: PG admin onayladığında credential'ı store'a HMAC-imzalı dayanıklı callback ile teslim eder. Onaysız teslim yoktur; approve aksiyonu MerchantKey döndürmez (MerchantId opak tutamaç olarak dönebilir).

**Why this priority**: Kullanıcının "en kritik" dediği direk (A). Sır hiç ekranda yoksa karışma/yanlış-paylaşım/phishing yüzeyi de yok.

**Independent Test**: Reveal sayfası + hosted form + resend-link route'ları 404; admin onayı credential döndürmüyor ama store callbackUrl'ine imzalı credential POST ediliyor; reissue yeni key'i de ekran yerine callback'le teslim ediyor.

**Acceptance Scenarios**:

1. **Given** Pending başvuru, **When** admin onaylar, **Then** Merchant Active olur, onay-aksiyonu dönüşü credential İÇERMEZ ve PG store'a HMAC-imzalı credential (MerchantId+Key) teslim eder.
2. **Given** credential teslimi ağ/HTTP hatasıyla düşer, **When** teslim başarısız, **Then** PG dayanıklı retry ile tekrar dener (041 emsali); sır log/trace/ekrana düşmez.
3. **Given** Active merchant'a reissue, **When** key yenilenir, **Then** yeni key ekran/RevealUrl yerine callback'le store'a gider ve eski key anında ölür (mevcut davranış korunur).
4. **Given** sökülen reveal/form/resend yüzeyleri, **When** eski route'a istek gelir, **Then** `404` döner.

---

### User Story 2 - MCP üzerinden hassas veri gelmez (Priority: P1)

PG'nin MCP (ve REST) tool dönüşleri merchant verisini gösterirken credential (MerchantKey) ve finansal/PII alanlarını (iban, taxNumber, identityNumber, email, gsm) İÇERMEZ. LLM/agent yalnız sır-olmayan veri (statü, ad, tip, adres, opak MerchantId tutamacı) görür.

**Why this priority**: İkinci temel direk (B). Hassas veri LLM yüzeyine girerse sohbet transkriptine/log'a sızar — kökten engellenir.

**Independent Test**: Tüm MCP tool dönüşleri + MerchantScoped query dönüşleri MerchantKey/iban/taxNumber/identityNumber içermiyor (denetim: 0 sızma); nöbetçi grep temiz.

**Acceptance Scenarios**:

1. **Given** herhangi MCP tool dönüşü (liste/detay), **When** merchant verisi döner, **Then** credential + finansal/PII İÇERMEZ.
2. **Given** MerchantScoped `GetMerchant`, **When** merchant kendi verisini okur, **Then** dönüş MerchantKey İÇERMEZ (scrub).

---

### Edge Cases

- Callback hiç başarılamaz (store down) → retry tükenince Active-ama-teslim-edilmedi; operatör sır-içermeyen log/kuyruktan görür; elle-reveal fallback YOK (bilinçli).
- Çift/gecikmeli callback → idempotent (correlation eşleme; aynı correlationId tekrar POST güvenli).
- Onaysız credential teslimi imkânsız (tek tetik = approve).
- Red → callback yok; store statüyü status sorgusuyla çeker.
- Bir tool/handler aggregate/gRPC objesini ham döndürürse sır sızar → dönüş DTO'su açıkça sır/PII-free (nöbetçi).

## Requirements *(mandatory)*

### Direk A — HTML ekran sır göstermez

- **FR-A1**: Merchant onboarding/credential sürecinde hiçbir insan-yüzeyi (HTML ekran, müşteri sohbeti) **MerchantKey**'i veya credential çiftini (MerchantId+MerchantKey birlikte) render ETMEMELİ. MerchantId tek başına sır DEĞİL (opak tutamaç); admin MCP'de tutamaç olarak görünebilir (FR-B1), ama MerchantKey hiçbir koşulda hiçbir yüzeyde görünmez.
- **FR-A2**: Credential store'a yalnız makine-handoff ile ulaşmalı: PG onayda MerchantId+MerchantKey'i store'un callback hedefine **HMAC-imzalı, dayanıklı/retry** teslim eder (041 `StoreCallbackDelivery` emsali). Onay aksiyonu credential DÖNDÜRMEZ; onaysız teslim YOK.
- **FR-A3**: Credential-sızdıran eski yüzeyler SÖKÜLMELİ: reveal sayfası (`/onboarding/reveal/{token}` + CredentialRevealLink + RevealCredentials), `admin_resend_credential_link`, hosted onboarding formu (OnboardingFormSessions + `/onboarding/{token}` + CreateFormSession + form HTML), onay-email (link gönderimi TÜMDEN kaldırılır — mail'le hiç link gitmez; store credential'ı callback'ten öğrenir). Reissue de ekransız (callback'le).
- **FR-A4**: Kayıt store-başlatan S2S ucundan gelmeli; finansal/PII yalnız S2S gövdede, ortak bootstrap key'le yetkili (kontrat: store `contracts/register-request.md` + `credential-callback.md`). Kayıt correlation + callback hedefini saklar (onaydan reissue'ya taşınır).

### Direk B — MCP'de hassas veri yok

- **FR-B1**: PG MCP/REST tool dönüşleri merchant verisi gösterirken credential (MerchantKey) + finansal/PII (iban, taxNumber, identityNumber, email, gsm) VERMEMELİ; dönüş DTO'su sır/PII-free. (Mevcut admin query tool'ları bunu sağlıyor; yeni/değişen dönüşler de uyar.)
- **FR-B2**: MerchantKey hiçbir query/tool/S2S dönüşünde, log'da veya trace'te bulunmamalı — yalnız sunucu belleği + callback gövdesi + mevcut `MerchantCreated`/`MerchantKeyReissued` iç-event'leri. MerchantScoped `GetMerchant` dönüşünden MerchantKey çıkarılmalı.

### Ortak

- **FR-C1**: Ele-alma politikası (render YOK / kayıt S2S / dönüş HMAC-callback / kullanım gRPC / LLM yalnız opak tutamaç) PG CLAUDE.md + docs/conventions'a yazılmalı; store ile tutarlı (aynı ADR).

### Key Entities

- **RegisterRequest**: Pending başvuru; EXTEND — correlationId (callback eşleme) + callbackUrl (teslim hedefi).
- **Merchant**: MerchantId + MerchantKey (onayda basar) + statü makinesi; callback hedefi onaydan reissue'ya taşınır.
- **Üç sır (ayrı ömür)**: BootstrapRegistrationKey (ortak, kayıt ucu) ≠ MerchantKey (per-merchant, ödeme) ≠ CallbackSecret (callback HMAC). Options (config).
- **Credential callback teslimi**: PG→store dayanıklı imzalı HTTP POST; 041 StoreCallbackDelivery aynası.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Onboarding'den reissue'ya tüm PG akışında MerchantKey (ve credential çifti) hiçbir HTML ekranda/müşteri sohbetinde görünmez (denetim: 0 render noktası); eski reveal/form/resend route'ları 404. (MerchantId opak tutamaç olarak admin MCP'de görünebilir — sır değil.)
- **SC-002**: Tüm MCP/REST tool + MerchantScoped query dönüşleri credential + finansal/PII-free (denetim: 0 sızma).
- **SC-003**: Onaylanan her başvurunun credential'ı store'a yalnız HMAC-imzalı callback'le ulaşır; onay aksiyonu credential döndürmez.
- **SC-004**: Politika tek ADR'den okunur; store + PG yüzeyleri aynı kurala uyar.

## Assumptions

- **Karar (kullanıcı, 2026-10-04):** makine-handoff + HMAC-callback; elle-reveal/form fallback YOK. Dayanak [[adr-mcp-control-plane-no-secret-return]].
- Tek tüketici ECommerce; hosted form öksüz → tam söküm güvenli (çoklu-site YAGNI).
- `MerchantCreated`/`MerchantKeyReissued` iç-event'leri (key taşır, Identity/Payment tüketir) DEĞİŞMEZ; 087 yalnız credential'ın insana-giriş yolunu (reveal → makine-callback) değiştirir.
- Callback gönderici 041 desenini aynalar (Wolverine durable outbox + hex HMAC-SHA256); yeni transport yok.
- 044 sensitive BFF + 048 sensitive-link 087 DIŞI (credential değil; 048 master'da implemente değil).
- Red credential teslim etmez; store reddi `GET /onboarding/status?email=` ile öğrenir.
- Canlı uçtan-uca doğrulama PG + store birlikte koşulunca (ayrı repo tamamlandı).