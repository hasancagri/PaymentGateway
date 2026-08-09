# Phase 0 Research: Onboarding Aggregate Sadeleştirme

Bu bir davranış-korumalı yapısal refactor; teknoloji seçimi yok. Aşağıdaki kararlar,
konsolidasyonun invariant'ları ve dış sözleşmeleri kırmadan nasıl yapılacağını çözer.
Tüm NEEDS CLARIFICATION kalemleri (spec clarify oturumunda) kapandı.

## D1 — Challenge'ı RegisterRequest'e gömme stratejisi

**Decision**: `RegisterRequest` challenge alanlarını (`ChallengeToken`, `ChallengeExpectedValue`,
`ChallengeExpiresAtUtc`) ve mevcut `ChallengeResult` (`ChallengeOutcome`) alanını taşır. Talep,
`Status = AwaitingDomainControl` ile ilk `submit_registration`'da doğar (descriptor alanları o an
doldurulur). Challenge davranışı RegisterRequest metotlarına iner:
- `CreateAwaiting(domain, descriptor, externalRef)` — AwaitingDomainControl talebi + ilk challenge
  bileti üretir (token/expected/expiry).
- `IssueChallenge(nowUtc)` — bilet süresi dolmuşsa yeni token/expected/expiry üretir (talep aynı).
- `VerifyChallenge(fetchedValue, nowUtc) : ResultDomain<ChallengeOutcome>` — eski
  `DomainControlChallenge.Verify` mantığı; `Passed` olunca `AwaitingDomainControl → Pending`
  geçişini KENDİ İÇİNDE yapar.

**Rationale**: Challenge tek bir domain-başvuru denemesine aittir; talep pre-challenge var olduğunda
challenge onun alanı olur. `ChallengeStatus` (Issued/Consumed/Expired) enum'una GEREK KALMAZ:
"Issued/not-passed" = `AwaitingDomainControl`; "Consumed/passed" = `Pending` (+ `ChallengeResult=Passed`);
"Expired" = re-issue. Böylece bir enum daha silinir.

**Alternatives considered**:
- Challenge'ı ayrı VO olarak RegisterRequest içinde tutmak → gereksiz sarmalama; düz alanlar yeter.
- `ChallengeStatus`'ı korumak → `Status` ile çift kaynak (tutarsızlık riski); reddedildi.

## D2 — Duplicate koruma kapsamı

**Decision**: Mükerrer koruma `AwaitingDomainControl` + `Pending` + `Approved` statülerini kapsar.
İkinci `submit_registration` aynı domain için AwaitingDomainControl talebi bulursa onu YENİDEN
KULLANIR (yeni talep açmaz); challenge'ı re-verify eder, süre dolmuşsa `IssueChallenge`.

**Rationale**: Talep artık challenge'dan önce doğduğu için, koruma AwaitingDomainControl'ü de
saymazsa aynı domain için yarım talep yığılır. Bul-veya-oluştur deseni idempotent tekrar-başvuruyu
korur (013 davranışı: "yayınla ve tekrar başvur").

**Alternatives considered**: Yalnız Pending/Approved koruma → yarım talep çoğalması; reddedildi.

## D3 — ActivationTicket'ı Merchant'a gömme

**Decision**: `Merchant` aktivasyon alanlarını taşır: `ActivationToken` (string),
`ActivationExpiresAtUtc` (DateTime?), `ActivationRedeemedAtUtc` (DateTime? — redeemed-flag).
Davranış:
- `IssueActivation(nowUtc)` — token (Guid "N") + 24h expiry üretir; onayda (`ApproveRegisterRequest`)
  çağrılır.
- `RedeemActivation(nowUtc) : ResultDomain` — tek-kullanım (`ActivationRedeemedAtUtc` doluysa RET) +
  TTL (`nowUtc > ActivationExpiresAtUtc` → RET). Başarıda redeemed işaretler ve `Provision()`
  etkisini (statü Provisioning sabit + `ActivatedAtUtc`) İÇİNDE uygular. `MerchantKey` yanıtta
  handler tarafından bir kez döndürülür (aggregate döndürmez).

**Rationale**: Bilet zaten tek bir merchant'a ait. Merchant üstünde tutmak `MerchantId` bağını
(yabancı anahtar) ortadan kaldırır; redeem, `ActivationToken` ile Merchant sorgusuna iner. TTL/tek-
kullanım invariant'ı `RedeemActivation` metodunda korunur (İlke II). `Provision()` mantığı redeem'e
gömülür — tek çağıran zaten redeem'di.

**Alternatives considered**:
- `Provision()`'ı ayrı public metot bırakmak → yalnız redeem çağırıyor; ayrı tutmak ölü yüzey.
  Redeem'e katlandı. (Gerekirse idempotent iç yardımcı kalır.)
- Aktivasyon key'ini `MerchantKey` ile birleştirmek → HAYIR; ikisi ayrı (key kalıcı kimlik,
  activation-token tek-kullanım teslim bileti). Ayrı alan kalır.

## D4 — OnboardingNotification kaldırma

**Decision**: Aggregate + `Create/MarkSent/MarkFailed` + `NotificationKind/NotificationStatus`
enum'ları silinir. `SubmitRegistration.NotifyAdminAsync` ve `ApproveRegisterRequest.SendActivationMailAsync`
yalnız `IMailSender.SendAsync` çağırır; sonuç `ILogger` ile loglanır (başarısızlıkta `LogWarning`).
Mail best-effort — akışı kesmez.

**Rationale**: Bu bir mail log'u, süreç durumu değil (spec US3). Dev aşamasında ayrı kayıt
gereksiz (memory: defansif kayıt tutma). Mail gönderim davranışı korunur.

**Alternatives considered**: Notification'ı event'e çevirmek → yeni altyapı; YAGNI, reddedildi.

## D5 — Dış sözleşme: AwaitingDomainControl görünürlüğü (clarify sonucu)

**Decision**: EKLEMELİ değişim. `SubmitRegistrationResponse` `RequestId`'yi `ChallengeRequired`
durumunda da doldurur (zaten alan var — şimdi AwaitingDomainControl talebinin Id'si set edilir).
`RegistrationStatusResponse`'a `Message` (Türkçe metin) alanı eklenir; `AwaitingDomainControl`
durumunda "sahiplik ispatı bekleniyor; şu değeri yayınla" benzeri metin döner. `Status` string'i
enum adını taşır (`AwaitingDomainControl`/`Pending`/`Approved`/`Rejected`). Yanıttaki mevcut
`Status` string sözleşmesi (`"ChallengeRequired"`/`"Pending"`) `submit_registration`'da KORUNUR.

**Rationale**: Clarify (Option B, text-first, on-demand): ECommerce belirsizlikte kalmaz; `RequestId`
ile "sürecim ne oldu?" sorabilir, metin yanıt alır. Poll/durum-makinesi dayatılmaz. Mevcut alan/
durumlar kaldırılmadığından kırıcı değil.

**Alternatives considered**: İç statüyü tamamen gizlemek (Option A) → ECommerce belirsizlikte kalır;
kullanıcı reddetti.

## D6 — Marten şema geçişi

**Decision**: Silinen üç document tipi için migration/temizlik YAZILMAZ; dev DB sıfırlanır. Yeni
alanlar (challenge + activation) mevcut document tiplerine eklenir; Marten şemayı auto-günceller.

**Rationale**: Proje memory (dev aşaması defansif migration yok). Eski onboarding verisi atılabilir.

**Alternatives considered**: Veri taşıma script'i → dev için gereksiz; reddedildi.

## D7 — Test taşıma

**Decision**: `DomainControlChallenge` ve `ActivationTicket` için mevcut saf domain testleri,
davranış yeni sahibine taşındığından `RegisterRequest` (challenge) ve `Merchant` (activation)
testlerine UYARLANIR; eski aggregate test dosyaları silinir. `OnboardingNotification` testleri (varsa)
silinir (loglama test edilmez).

**Rationale**: Davranış korunumu (SC-004) yeni sahiplerin birim testleriyle kanıtlanır; test
konvansiyonu (saf domain) aynen geçerli.