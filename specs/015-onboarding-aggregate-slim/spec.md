# Feature Specification: Onboarding Aggregate Sadeleştirme (5 → 2)

**Feature Branch**: `015-onboarding-aggregate-slim`

**Created**: 2026-08-09

**Status**: Draft

**Input**: User description: "013 merchant onboarding sürecini sadeleştir: 5 aggregate'ten 2'ye indir. RegisterRequest ve Merchant kalır. DomainControlChallenge, ActivationTicket, OnboardingNotification aggregate'leri silinir. DomainControlChallenge alanları RegisterRequest'e, ActivationTicket alanları Merchant'a gömülür; OnboardingNotification silinir (mail ILogger + Mail.Mcp ile kalır). Amaç: süreç tek yerden (RegisterRequest.Status enum) okunabilsin, class sayısı azalsın. Dev aşaması: migration yok, DB sıfırlanabilir."

## Problem

013 merchant onboarding, tek bir "site kaydolsun" işini **beş aggregate**'e dağıtmıştır:
`RegisterRequest`, `DomainControlChallenge`, `ActivationTicket`, `OnboardingNotification`,
`Merchant`. Sürecin nerede olduğunu görmek için birden fazla aggregate + handler + event
consumer + MCP tool okumak gerekir; tek bir "bu başvuru hangi adımda?" görünümü yoktur.
Bu, bakım maliyetini ve bilişsel yükü artırır (anayasa YAGNI + CLAUDE.md aggregate-klasör
kuralına rağmen parça sayısı gereğinden fazla).

Bu feature **davranışı değiştirmeden** onboarding sürecini iki aggregate'e indirir:
- **RegisterRequest** — tüm başvuru süreci (challenge dahil), tek statü enum'undan okunur.
- **Merchant** — kalıcı sonuç (aktivasyon bileti dahil).

Silinen üç aggregate'in davranışı yok olmaz; sahiplerine (RegisterRequest / Merchant) taşınır
ya da (OnboardingNotification) hafif loglamaya iner.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Challenge RegisterRequest'e gömülür (Priority: P1)

Bir merchant adayı başvurduğunda, domain-control challenge (sahiplik ispatı) artık ayrı bir
`DomainControlChallenge` aggregate'i değil, `RegisterRequest`'in kendi alanları ve statüsüdür.
Talep, challenge geçmeden ÖNCE `AwaitingDomainControl` statüsünde doğar; kanıt geçince
`Pending`'e ilerler.

**Why this priority**: Konsolidasyonun kalbi. Challenge, bir domain-başvuru denemesine aittir;
onu talebe gömmek "envai çeşit aggregate" şikâyetini en çok azaltan adımdır ve sürecin tek
statü enum'undan okunmasını sağlar.

**Independent Test**: Bir başvuru gönder → `RegisterRequest` `AwaitingDomainControl` statüsünde
oluşur, token+beklenen değer taşır. Beklenen değeri yayınlayıp tekrar başvur → aynı talep
`Pending`'e geçer. `DomainControlChallenge` aggregate'i sistemde artık yoktur.

**Acceptance Scenarios**:

1. **Given** hiçbir aktif talep yok, **When** aday geçerli bir başvuru gönderir ve beklenen
   değeri henüz yayınlamamıştır, **Then** `RegisterRequest` `AwaitingDomainControl` statüsünde
   oluşur ve yanıt token + beklenen değer + yayın yolunu döner (challenge yeniden istenir).
2. **Given** `AwaitingDomainControl` statüsünde bir talep var, **When** aday beklenen değeri
   doğru yayınlayıp başvuruyu tekrarlar, **Then** aynı talep `Pending`'e geçer (yeni talep
   açılmaz), süreç değişmeden devam eder.
3. **Given** bir domain için `AwaitingDomainControl` veya `Pending` talep var, **When** aynı
   domain için yeni başvuru gelir, **Then** mükerrer koruma devreye girer (yeni talep açılmaz).
4. **Given** challenge bileti süresi dolmuş, **When** aday tekrar başvurur, **Then** yeni bilet
   üretilir (talep aynı kalır, süre yenilenir).

---

### User Story 2 - ActivationTicket Merchant'a gömülür (Priority: P2)

Aktivasyon bileti (tek-kullanım key teslim bileti) artık ayrı bir `ActivationTicket`
aggregate'i değil, `Merchant`'ın alanlarıdır (aktivasyon key'i + son kullanma + kullanıldı
işareti). Bilet üretimi ve redeem'i `Merchant` davranış metotlarıdır.

**Why this priority**: İkinci en büyük konsolidasyon. Aktivasyon bileti zaten tek bir
merchant'a aittir; onu merchant'a gömmek bir aggregate daha kaldırır. Redeem'in tek-kullanım
ve süre invariant'ı korunur.

**Independent Test**: Bir talebi onayla → merchant Provisioning olarak doğar ve aktivasyon
bileti alanları dolar (key + süre + kullanılmadı). Redeem ucunu çağır → key bir kez döner,
ikinci çağrı reddedilir. `ActivationTicket` aggregate'i sistemde artık yoktur.

**Acceptance Scenarios**:

1. **Given** bir `Pending` talep, **When** admin onaylar, **Then** merchant Provisioning
   statüsünde doğar ve tek-kullanımlık aktivasyon key'i + son kullanma tarihi merchant üstünde
   üretilir.
2. **Given** kullanılmamış, süresi geçmemiş bir aktivasyon key'i, **When** redeem edilir,
   **Then** key bir kez döner, merchant provision edilir (`MerchantProvisioned` yayınlanır) ve
   key "kullanıldı" işaretlenir.
3. **Given** bir kez redeem edilmiş key, **When** aynı key tekrar redeem edilir, **Then** işlem
   reddedilir (tek-kullanım korunur).
4. **Given** süresi geçmiş bir aktivasyon key'i, **When** redeem edilir, **Then** işlem
   reddedilir (süre invariant'ı korunur).

---

### User Story 3 - OnboardingNotification silinir (Priority: P3)

Deterministik mail durum kaydı (`OnboardingNotification` aggregate'i) kaldırılır. Onboarding
mailleri gönderilmeye devam eder (admin "yeni başvuru", aktivasyon key teslimi) ancak ayrı bir
durum aggregate'ine YAZILMAZ; gönderim sonucu `ILogger` ile loglanır, mail Mail.Mcp üzerinden
gider.

**Why this priority**: En zayıf gerekçeli aggregate (durum değil, log). Dev aşamasında ayrı
kayıt tutmaya gerek yok; kaldırmak üçüncü aggregate'i de eler. Mail gönderimi davranışı korunur.

**Independent Test**: Bir başvuru admin'e bildirim maili tetikler → mail gönderilir, sonuç
loglanır, sistemde `OnboardingNotification` dokümanı OLUŞMAZ.

**Acceptance Scenarios**:

1. **Given** yeni bir başvuru oluşur, **When** admin bildirim maili tetiklenir, **Then** mail
   gönderilir ve gönderim sonucu (başarılı/başarısız) loglanır; ayrı bir bildirim dokümanı
   yazılmaz.
2. **Given** mail gönderimi başarısız olur, **When** hata oluşur, **Then** onboarding akışı
   kesilmez (mail best-effort'tur), hata loglanır.

---

### Edge Cases

- **Talep pre-challenge var olurken mükerrer:** Talep artık challenge'dan önce
  (`AwaitingDomainControl`) doğduğundan, mükerrer koruma hem `AwaitingDomainControl` hem
  `Pending` (hem `Approved`) statülerini kapsamalıdır; aksi halde aynı domain için birden fazla
  yarım talep açılabilir.
- **Karar kapıları:** `Approve`/`Reject` yalnız `Pending` talepte çalışır; `AwaitingDomainControl`
  bir talep onaylanamaz/reddedilemez (henüz sahiplik ispatı yok).
- **Aktivasyon key'i kalıcılığı:** Key merchant üstünde tutulur; redeem'de bir kez döndükten
  sonra tekrar okunamaması (yalnız kullanıldı-işareti + son-kullanma) korunmalıdır.
- **Statü listeleme/sorgu:** Talep listeleme/detay uçları yeni `AwaitingDomainControl` statüsünü
  tanımalı ve filtreleyebilmelidir.

## Requirements *(mandatory)*

### Functional Requirements

**Aggregate konsolidasyonu**

- **FR-001**: Sistem onboarding sürecini yalnız İKİ aggregate ile modellemek ZORUNDADIR:
  `RegisterRequest` (tüm başvuru süreci) ve `Merchant` (kalıcı sonuç).
- **FR-002**: `DomainControlChallenge` aggregate'i KALDIRILMALIDIR; challenge davranışı ve
  verisi (token, beklenen değer, son kullanma, sonuç) `RegisterRequest`'e taşınmalıdır.
- **FR-003**: `ActivationTicket` aggregate'i KALDIRILMALIDIR; aktivasyon bileti verisi
  (key, son kullanma, kullanıldı işareti) ve davranışı (üret/redeem) `Merchant`'a taşınmalıdır.
- **FR-004**: `OnboardingNotification` aggregate'i KALDIRILMALIDIR; onboarding mailleri
  gönderilmeye devam etmeli ancak ayrı durum kaydı OLUŞTURULMAMALIDIR (sonuç loglanır).

**RegisterRequest statü akışı**

- **FR-005**: `RegisterRequest` statü akışı `AwaitingDomainControl → Pending →
  Approved/Rejected` OLMALIDIR. Talep, challenge geçmeden önce `AwaitingDomainControl`
  statüsünde OLUŞUR.
- **FR-006**: Challenge sahiplik ispatı geçtiğinde (beklenen değer doğrulanınca), aynı
  `RegisterRequest` `Pending`'e ilerlemek ZORUNDADIR; süreç yeni bir talep açmadan devam eder.
- **FR-007**: Mükerrer koruma, aynı domain için `AwaitingDomainControl`, `Pending` veya
  `Approved` statüsünde talep varsa yeni talep açılmasını ENGELLEMELİDİR.
- **FR-008**: `Approve` ve `Reject` yalnız `Pending` statüsündeki talepte GEÇERLİ olmalıdır;
  `AwaitingDomainControl` talep karar alamaz.
- **FR-009**: Challenge bileti süresi dolduğunda, aynı talep üzerinde yeni bilet
  (token + beklenen değer + süre) üretilebilmelidir; talep yeniden oluşturulmaz.

**Merchant aktivasyon bileti**

- **FR-010**: Talep onaylandığında `Merchant` Provisioning statüsünde doğmalı ve tek-kullanımlık
  aktivasyon key'i + son kullanma tarihi merchant üstünde üretilmelidir.
- **FR-011**: Aktivasyon key'i redeem'i tek-kullanım ve süre invariant'larını KORUMALIDIR:
  kullanılmamış + süresi geçmemiş key bir kez teslim edilir, ardından "kullanıldı" işaretlenir;
  ikinci redeem veya süresi geçmiş key reddedilir.
- **FR-012**: Başarılı redeem, mevcut davranışı (merchant provision edilir, `MerchantProvisioned`
  yayınlanır, key bir kez döner) KORUMALIDIR.

**Davranış korunumu**

- **FR-013**: Dış gözlemlenebilir onboarding davranışı (başvuru → challenge → pending → onay →
  aktivasyon → aktif; ilgili MCP/HTTP uçlarının girdi/çıktı sözleşmeleri) DEĞİŞMEMELİDİR;
  yalnız iç aggregate yapısı sadeleşir. (İstisna: yeni `AwaitingDomainControl` statüsü, daha
  önce "talep yok + ChallengeRequired" ile temsil edilen ara durumu artık kalıcı bir talep
  olarak yansıtır.)
- **FR-014**: `Merchant.TryActivate()` üç-koşul kapısı (settlement + komisyon grid + ReturnUrl)
  ve komisyon-grid fanout tüketimi DEĞİŞMEDEN çalışmalıdır.
- **FR-015**: Kaldırılan aggregate'lere ait tüm dosyalar (aggregate kökü, Features slice'ları,
  MCP tool referansları, endpoint'ler) ya silinmeli ya da kalan iki aggregate'e taşınmalıdır;
  ölü referans/derleme hatası KALMAMALIDIR.

### Key Entities

- **RegisterRequest** (kalır, genişler): Başvuru sürecinin tamamı. Mevcut alanlar (domain,
  legalName, taxId, contactEmail, webhookUrl, challengeResult, status, reviewedAt, reviewNote,
  createdMerchantId, externalRef) + challenge alanları (token, beklenen değer, son kullanma).
  Statü: `AwaitingDomainControl | Pending | Approved | Rejected`.
- **Merchant** (kalır, genişler): Kalıcı sonuç. Mevcut Provisioning/Active döngüsü + aktivasyon
  bileti alanları (key, son kullanma, kullanıldı işareti) ve `IssueActivation`/`RedeemActivation`
  davranışları.
- **DomainControlChallenge** (SİLİNİR): Alanları + Issue/Verify davranışı RegisterRequest'e taşınır.
- **ActivationTicket** (SİLİNİR): Alanları + Issue/Redeem davranışı Merchant'a taşınır.
- **OnboardingNotification** (SİLİNİR): Karşılığı yok; mail gönderimi loglama ile korunur.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Onboarding sürecini modelleyen aggregate sayısı 5'ten 2'ye iner (`grep -rlE
  "class .*: AggregateRoot" src/services/Merchant.Api/Domains` çıktısında onboarding'e ait
  yalnız RegisterRequest + Merchant kalır; DomainControlChallenge/ActivationTicket/
  OnboardingNotification dosyaları YOKTUR).
- **SC-002**: Bir başvurunun hangi adımda olduğu tek bir yerden — `RegisterRequest.Status` —
  okunabilir (AwaitingDomainControl / Pending / Approved / Rejected).
- **SC-003**: Mevcut 013 quickstart onboarding senaryoları (başvuru → challenge → onay →
  aktivasyon → aktif) davranış olarak aynen geçer; dış uç sözleşmeleri kırılmaz.
- **SC-004**: Çözüm sıfır derleme hatasıyla derlenir ve mevcut Merchant.Api saf domain birim
  testleri (aktarılan/uyarlanan challenge + aktivasyon testleri dahil) yeşildir; ölü kod/
  referans kalmaz.

## Assumptions

- **Dev aşaması, migration yok**: Geriye-uyum/veri migration üretilmez; şema değiştiğinde DB
  sıfırlanabilir (proje memory: dev aşaması defansif migration yok).
- **Submit sözleşmesi bu feature'da değişmez**: Başvuru girdisi (descriptor linki + challenge)
  ve KYC alanları bu feature kapsamında yeniden tasarlanmaz; yalnız aggregate yapısı sadeleşir.
  (Brainstorm'da konuşulan "ad+link / mail ile eksik tamamlama" ayrı bir feature'dır.)
- **Anayasa uyumu**: İki aggregate de zengin (private setter + statik fabrika + davranış)
  kalır (İlke II); Vertical Slice + Result pattern korunur; challenge/aktivasyon davranışları
  handler'a sızmaz, aggregate metotlarında yaşar.
- **Aggregate-klasör kuralı**: Kalan iki aggregate CLAUDE.md aggregate-klasör kuralına uyar
  (`Domains/` altındaki her klasör tek `: AggregateRoot`; challenge/aktivasyon enum'ları ve
  ValueObject'leri ilgili aggregate klasörü altında durur).
- **Statü-yetki etkilenmez**: Merchant token verme (Provisioning/Active kademeli yetki, 013)
  bu değişiklikten etkilenmez; aktivasyon key'i merchant'a taşınsa da `MerchantProvisioned`
  event sözleşmesi korunur.