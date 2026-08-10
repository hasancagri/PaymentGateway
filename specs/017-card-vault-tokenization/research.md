# Phase 0 Research: Card Vault / Tokenization

Teknik bilinmeyenler ve karar gerektiren noktalar; her biri Decision / Rationale / Alternatives.

## R1 — Yazım abstraksiyonu: ICardVault write mi, slice mi?

- **Decision**: `ICardVault` **resolve-only** kalır (`ResolveCardInfoAsync`). Tokenize/update/revoke
  **Command slice** olarak `IDocumentSession` ile yazılır. Kullanıcı notundaki "ICardVault write
  eklenir (StoreCardAsync/RevokeAsync/UpdateAsync)" fikri UYGULANMAZ.
- **Rationale**: Anayasa III — repository deseni YASAK; handler kalıcılığa doğrudan `IDocumentSession`
  ile erişir. `ICardVault`'a write eklemek onu repository'ye çevirir. Resolve, ödeme akışının
  query-side portudur (mevcut tüketici `QuoteInstallmentsForSessionCommandHandler`), o yüzden kalır.
- **Alternatives**: ICardVault'a write eklemek (reddedildi — repository ihlali).

## R2 — Merchant token Payment audience'ına nasıl girer? (auth düzlemi) — DÜZELTİLDİ (analyze C1)

- **Decision**: Vault uçları **yeni capability scope `payment.vault`** + `AuthorizationPolicies.
  MerchantScoped` ile korunur; route `merchants/{merchantId:guid}/vault/cards`. Identity.Server
  **Active** merchant demetine `payment.vault` eklenir (statü-kapılı; Provisioning ALMAZ). Payment.Api
  JwtBearer merchant token'ını yalnız `payment.vault` uçları için tanır. `payment.write` (Payment
  `/mcp` agent yüzeyi + `/pos-accounts`) merchant'a **VERİLMEZ**.
- **Rationale (İLK KARAR DÜZELTİLDİ)**: İlk tur `payment.write` yeniden kullanımını seçmişti; analyze
  C1 bunun `/mcp` ve `/pos-accounts`'u (payment.write, MerchantScoped YOK) merchant token'ına açtığını
  gösterdi (yetki deliği, anayasa V). Anayasa yalnız **merchant/statü-başına** scope çoğaltmayı yasaklar;
  **capability** scope (yetki türü) serbesttir — `mail.send`/`document.generate` precedent'i. `payment.vault`
  yeni bir yetki TÜRÜdür, kimlik/statü kopyası değil → yasağa girmez. Anayasa V "Active tam demet"
  ödeme düzlemini açar; bunu dar bir capability scope'la yapmak "hiçbir korunması gereken uç açıkta
  bırakılmaz" ilkesini korur. Charge fail-closed: Provisioning `payment.vault` almaz (FR-017).
- **Alternatives**: (a) `payment.write` yeniden kullanımı — **reddedildi** (C1: mcp/pos aşırı-yetki).
  (b) Diğer payment.write uçlarına MerchantScoped ekleyip merchant'ı dışlamak — reddedildi (kırılgan,
  her yeni payment.write ucunda tekrar gerekir; capability scope temiz sınır). (c) Vault'u admin/agent
  client'ına açmak — reddedildi (senaryo: ECommerce merchant kimliğiyle çağırır).
- **Uygulama notu**: `AuthorizationScopes.PaymentVault = "payment.vault"` sabiti Common'a eklenir;
  Identity.Server scope kayıt listesine + Active merchant demetine girer.

## R3 — Resolve anında cross-merchant enforcement

- **Decision**: **Yazım tarafı** (tokenize/update/revoke) token↔merchant eşleşmesini TAM zorlar
  (route `{merchantId}` + `merchant_id` claim, MerchantScoped fail-closed). **Resolve** (ödeme akışı)
  bu feature'da yalnız **statü** kontrolü yapar (Revoked → RET). Ödeme-anı cross-merchant eşleşmesi,
  `PaymentSession`'ın `merchantId` taşıdığı charge feature'ına (007 devamı) ertelenir.
- **Rationale**: Mevcut `PaymentSession.Create(cardToken, cartAmount)` merchantId taşımıyor
  (decisions_007: stateless/merchantId itirazı). Resolve'a merchantId enjekte etmek 007 kapsam
  genişlemesi olur. Yazım tarafı tam korunduğu için token zaten doğru merchant'a bağlı üretiliyor;
  ödeme-anı ikinci kapı charge ile gelir.
- **Alternatives**: PaymentSession'a merchantId eklemek şimdi — reddedildi (007 scope creep). Spec
  SC-003 tam ödeme-anı kapsaması charge'la kapanır; bu feature yazım-tarafı + Revoked ile karşılar.
- **Not**: Bu bilinçli erteleme quickstart + spec Assumptions ile uyumlu; tasks'ta açık işaretlenir.

## R4 — PAN enc-at-rest (dev simüle) ve resolve'da PAN gerekmez

- **Decision**: `IPanProtector` (dev `DevPanProtector`, reversible, `ISingletonDependency`) PAN'ı
  `EncryptedPan` alanına yazar. **Resolve PAN'ı çözmez** — yalnız saklanan `Bin`'i kullanıp
  `ResolveBinCard.Resolve(bin)` çağırır. PAN'ın decrypt'i yalnız gerçek charge (CP.VPOS) anında
  gerekir → bu feature'da PAN yazılır ama okunmaz.
- **Rationale**: En az yüzey: PAN write-only tutulur, sızma yüzeyi minimum. Gerçek KMS/HSM sonradan
  `IPanProtector` ardında değiştirilir (kapsam dışı). BIN/last4/brand ayrı queryable alanlar.
- **Alternatives**: PAN'ı düz saklamak — reddedildi (enc-at-rest simülasyonu bile sınır davranışını
  doğru modeller). Gerçek KMS — kapsam dışı.

## R5 — bin/last4/brand türetimi

- **Decision**: Gateway tokenize anında PAN'dan **kendi türetir**: `Bin` (ilk 6–8), `Last4` (son 4),
  `Brand` (PAN prefix'inden saf `BrandDetector`: Visa `4`, Mastercard `5x`/`2x`, Amex `34/37`,
  Troy `9792...`, aksi `Unknown`). Saf yardımcılar `PanTools` (LuhnValidator, BinExtractor,
  BrandDetector). ECommerce kendi last4/brand'ini ayrıca kendi çıkarır (bağımsız, kullanıcı kuralı).
- **Rationale**: Resolve `Bin`'e ihtiyaç duyar (BankRouter beslemesi); last4/brand denetim/gösterim
  ve gelecekteki Fraud sinyali. Saf fonksiyon → domain birim testine uygun.
- **Alternatives**: Brand'i BinCard katalog lookup'ından almak — reddedildi (her BIN katalogda yok;
  prefix türetimi yeter ve deterministik). Bin uzunluğu: 6 hane tabanlı (ResolveBinCard 6-fallback
  ile uyumlu), 8 varsa saklanır.

## R6 — StoredCard Marten identity

- **Decision**: Marten identity = `Token` (string), `opts.Schema.For<StoredCard>().Identity(x =>
  x.Token).Index(x => x.MerchantId)`. `LoadAsync<StoredCard>(token)` doğrudan.
- **Rationale**: BinCard precedent (`.Identity(x => x.BinNumber)`). Token doğal anahtar, opak,
  benzersiz. Guid Id anlamsız kalırdı.
- **Alternatives**: Guid Id + unique index Token — çalışır ama fazladan lookup; precedent Token-identity.

## R7 — SimulatedCardVault geleceği + mevcut 007 quickstart token'ları

- **Decision**: `SimulatedCardVault` gerçek `StoredCard` çözümüne dönüştürülür (fixture map kalkar).
  Ad `SimulatedCardVault` → semantik artık gerçek; yeniden adlandırma opsiyonel (`StoredCardVault`).
  Mevcut 007 quickstart sabit token'ları (`tok_credit_taksitli` vb.) artık geçersiz → 007 quickstart
  akışı önce tokenize edip dönen token'ı kullanacak şekilde güncellenir (dev-phase-no-defensive:
  fixture seed'i taşımıyoruz).
- **Rationale**: Tek doğruluk kaynağı StoredCard; iki yol (fixture + gerçek) tutulmaz. Dev aşaması
  geriye-uyum migration üretmez ([[feedback_dev_phase_no_defensive_migrations]]).
- **Alternatives**: Fixture token'ları StoredCard olarak seed etmek — reddedildi (gereksiz defansif;
  quickstart tokenize ile kendini besler).

## R8 — Update PAN taşımaz / Revoke idempotent

- **Decision**: `UpdateDetails(expiry, holderName)` yalnız bu ikisini değiştirir; PAN/token/bin/last4/
  brand immutable. `Revoke()` idempotent (zaten Revoked → Ok). Revoked kayıt update/resolve'da RET.
- **Rationale**: Onaylı kurallar 1/4/5. PAN değişimi = sil+yeni tokenize (yeni token, FR-013/FR-014).
- **Alternatives**: Update'in PAN kabul etmesi — reddedildi (immutable PAN kararı).