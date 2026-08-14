# Research: iyzico Saklı Kart'a Geçiş (Model A) — 032

**Date**: 2026-08-14 | **Spec**: [spec.md](spec.md)

## R1 — iyzico Saklı Kart wire tipleri HAZIR + çekirdek KANITLI

**Decision**: `Provider/StoredCards/` altında tam set uyuyor: `Card.Create(CreateCardRequest,
ProviderOptions)` → POST `/cardstorage/card`; `Card.Delete(DeleteCardRequest, ProviderOptions)`
→ DELETE `/cardstorage/card`; `CreateCardRequest{ExternalId, Email, CardUserKey?, Card:
CardInformation{CardNumber, ExpireYear, ExpireMonth, CardHolderName, CardAlias}}`; yanıt
`Card{CardUserKey, CardToken, BinNumber, LastFourDigits, CardType, CardAssociation, ...}`.

**Rationale**: Provider çekirdeği (HashGeneratorV2 imza + RestHttpClientV2 + ProviderResourceV2
header montajı) spike'la KANITLANDI — sandbox `retrieveInstallmentInfo` → `Status=success, HTTP 200`
(2026-08-14). İmza/bağlantı çalışıyor; card storage aynı çekirdekten geçer.

## R2 — KRİTİK: sözleşme buyer kimliği taşımıyor → per-kart cardUserKey (FR-004 gevşetilir)

**Decision**: Her saklanan kart KENDİ (cardUserKey, cardToken) çiftini alır — `Card.Create`'e
mevcut `CardUserKey` GEÇİLMEZ; iyzico her çağrıda yeni cardUserKey mint eder. Aynı son-kullanıcının
kartlarını tek cardUserKey altında gruplama ERTELENİR.

**Rationale**: 031 dış sözleşmesi `{pan, expiry, holderName="CARD HOLDER"}` — **buyer/e-posta
kimliği YOK** (ECommerce `GatewayCardTokenizer` kaynak: satır 66, holderName sabit). Gateway aynı
son-kullanıcıyı ayırt edemez. FR-004'ün gruplaması FR-008 (sıfır dokunuş) ile çakışır. **Ödeme için
gruplama ŞART DEĞİL** — CVC-siz ödeme (cardUserKey, cardToken) çiftiyle yapılır; her kart kendi
çiftini taşırsa ödeme çalışır (SC-005 karşılanır). Gruplama, ileride ECommerce buyer kimliği taşırsa
eklenir (o zaman `Card.Create`'e mevcut cardUserKey geçilir).

**Alternatives considered**: (a) sözleşmeye buyer/email ekle → FR-008 sıfır-dokunuş kırılır, red;
(b) merchant e-postasını buyer sayıp tek cardUserKey → tüm merchant kartları tek kullanıcıda birikir,
YANLIŞ (farklı son-kullanıcılar karışır), red.

**Spec etkisi**: FR-004 "aynı kullanıcının ikinci kartı mevcut cardUserKey'e eklenir" → bu feature'da
KARŞILANMAZ (buyer kimliği yok); per-kart bağımsız cardUserKey uygulanır. SC-003 (kullanıcı-kimliği
tekilliği) bu feature kapsamından çıkar — gruplama ertelenen iş.

## R3 — iyzico `Email` gerekli: sabit placeholder

**Decision**: `CreateCardRequest.Email` iyzico'nun yeni cardUserKey üretmesi için gerekli (email VEYA
mevcut cardUserKey'den biri şart). Buyer kimliği olmadığından **sabit/sentetik e-posta** kullanılır
(ör. `vault+{merchantId}@dropshop.local` veya `card-holder@dropshop.local`). ExternalId = üretilen
opak token (izleme). CardAlias = merchant + kısa etiket.

**Rationale**: iyzico email'i yalnız cardUserKey mint ederken kullanır; per-kart cardUserKey
modelinde grupla­ma yapmadığımızdan email'in benzersizliği önemli değil. Gerçek buyer email'i
sözleşmeye girerse buraya bağlanır.

## R4 — 031 yeniden-yazımı: StoredCard iç + slice'lar; AES/Luhn kalkar

**Decision**: `StoredCard` aggregate `EncryptedPan` alanını KALDIRIR, `CardUserKey` + `CardToken`
(sağlayıcı kimlikleri) ekler. `Create` fabrikası artık lokal Luhn/expiry doğrulama + AES yapmaz —
handler iyzico `Card.Create` çağırır, dönen kimlikler + gösterim alanlarıyla aggregate'i kurar.
`DevPanProtector` + `LuhnValidator` SİLİNİR (iyzico doğrular). `BrandDetector`/`BinExtractor`/
`Last4Extractor` KALIR (fallback: iyzico gösterim alanını vermezse; ama normalde iyzico
`BinNumber`/`LastFourDigits`/`CardAssociation` döner → onları tercih et). `Token` (opak `card_`) +
soft `Revoke` + Marten identity KORUNUR.

**Rationale**: Model A'da PAN gateway'e hiç girmez → AES/Luhn anlamsız. Aggregate hâlâ zengin:
`Create` invariant'ları (zorunlu alan, sağlayıcı kimlikleri) + `Revoke`.

## R5 — Tokenize handler artık iyzico çağırır (async, fail-closed)

**Decision**: `TokenizeCardCommandHandler` iyzico `Card.Create` çağırır (`ProviderOptions` inject);
başarısızsa (Status != success VEYA exception) `INVALID_OPERATION_ERROR` döner, aggregate KAYDEDİLMEZ
(fail-closed — FR-007). Başarıda: aggregate `StoredCard.Create(merchantId, cardUserKey, cardToken,
bin, last4, brand, expiry, holder)` + Store. `[Transactional]` korunur ama iyzico çağrısı DB
transaction'ı DIŞINDA (yan etki): önce iyzico, başarılıysa Store. RevokeCard: iyzico `Card.Delete`
(cardUserKey+cardToken) best-effort → yerel soft revoke (FR-006; sağlayıcı hatası yerel iptali
bloklamaz).

**Rationale**: FR-007 fail-closed + FR-006 fail-open silme; 031 handler yapısı korunur, gövdeye
iyzico çağrısı eklenir.

## R6 — ProviderOptions: Options pattern + user-secrets (sandbox key git'e girmez)

**Decision**: `ProviderOptions` (ApiKey/SecretKey/BaseUrl) — `Options/IyzicoProviderSettings`
POCO + `AddOptionsExt` (`BindConfiguration` + `ValidateOnStart`); handler düz POCO inject eder
(constitution config kuralı). Sandbox key/secret **user-secrets**'a (`dotnet user-secrets set`
Payment.Api) — appsettings'e dev placeholder, gerçek değer git-dışı. `BaseUrl` =
`https://sandbox-api.iyzipay.com`.

**Rationale**: Constitution "Config — Options pattern"; FR-009 secret koda/git'e girmez.
`ProviderOptions` mevcut tip (Provider/), onu POCO olarak Options'a bağlarız.

## R7 — Veri: 031 kayıtları taşınmaz, paymentDb storedcard sıfırlanır

**Decision**: 031 `EncryptedPan`'li kayıtlar yeni şekle uymaz; `mt_doc_storedcard` truncate + Marten
yeni şekli oluşturur. Marten kaydı (`Schema.For<StoredCard>().Identity(Token).Index(MerchantId)`)
KORUNUR (031 canlı fix).

## R8 — Test: iyzico çağrısı mock/entegrasyon; saf domain kalanı korunur

**Decision**: Aggregate saf kısmı (Create kimliklerle, Revoke idempotent) birim test edilir
(iyzico'suz — artık Luhn/AES yok, `Create` sağlayıcı kimliklerini alır). iyzico `Card.Create`/
`Delete` gerçek çağrısı **quickstart canlı** ile doğrulanır (sandbox); handler-level mock testi
opsiyonel (Provider statik metotlar mock'lanamaz — canlı doğrulama esas). 031'in Luhn/normalize
testleri SİLİNİR (mantık iyzico'ya geçti).
