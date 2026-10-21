# Phase 0 Research: NonSecure Banka Çekim Dilimi

Teknik bağlamda açık (NEEDS CLARIFICATION) kalmadı; kararlar önceki keşif (pos-master + PG altyapı + konvansiyon taraması) ve onaylı plandan türetildi. Konsolide kararlar:

## D1 — Banka adapter deseni: per-banka ResponseMapper arkası ortak port

- **Decision**: Ortak `IBankPaymentAdapter` port + banka başına adapter; her adapter kendi wire istek/cevabını private nested tutar ve private `ResponseMapper` ile normalize `BankChargeOutcome`'a çevirir.
- **Rationale**: Anayasa İlke VII + CLAUDE.md "paylaşılan SDK yok, wire slice'ta nested". pos-master (`~/Desktop/pos-master`) bunu birebir doğruluyor: `ResponseDataMapperInterface` (TX_APPROVED/TX_DECLINED) + `AbstractResponseDataMapper` (normalize default şekil) + 16 banka `{Banka}PosResponseDataMapper` (her biri kendi ham kodunu çevirir: Garanti `'00'`, Akbank `'VPS-0000'`, Iyzico `'success'`). NonSecure (`mapPaymentResponse`) vs 3DS (`map3DPaymentData`) ayrı metot.
- **Alternatives considered**: Paylaşılan banka SDK → İlke VII reddi (bir bankanın değişimi hepsine bulaşır). Tek dev-if-else adapter → taksonomi + routing büyüyünce kırılır.

## D2 — Normalize sözlük: outcome VO + 4-sınıf red taksonomisi

- **Decision**: `BankChargeOutcome` VO (`IsApproved`, `DeclineClass?`, `ProviderRef?`, `AuthCode?`, `RawCode`). `DeclineClass ∈ {IssuerDecline, TechnicalFailure, ConfigError, FraudDecline}`. Red `Ok(outcome)` ile taşınır, `ResultDomain.Error`'a eşlenmez.
- **Rationale**: İlke IV outcome-enum kuralı ("retry-able Failed teknik hata değildir"). Storm Ders-16 taksonomisi. `RawCode` pos-master `proc_return_code`/`error_code` aynası (denetim).
- **Alternatives considered**: Boolean success/fail → failover kararı için yetersiz (hangi red tipi?). Exception → İlke IV reddi.

## D3 — PaymentAttempt: PaymentLink altında child entity

- **Decision**: `PaymentAttempt` = `PaymentLink` altında nested child entity (AggregateRoot DEĞİL, base almaz); private `List<PaymentAttempt>`, okuma `IReadOnlyList`, mutasyon yalnız `PaymentLink.RecordAttempt`.
- **Rationale**: İlke II (invariant + koleksiyon aggregate içinde). Deneme linkten bağımsız yaşamı/kimliği yok. Ayrı aggregate → "tek AggregateRoot/klasör" ihlali + gereksiz cross-aggregate eventual consistency.
- **Alternatives considered**: Ayrı `PaymentAttempt` aggregate → reddedildi. Attempt'siz tek alan → N-deneme + denetim/mutabakat kaybı.

## D4 — BankChargeRouter: domain-service (Process/Saga değil), 2-aşama + canlı failover

- **Decision**: `BankChargeRouter` = aggregate kökünde domain-service, `IScopedDependency`, `IEnumerable<IBankPaymentAdapter>` + `BankRoutingOptions` + `PosAccountOptions` + BinCard lookup enjekte. Önce aday sıralar (D9), sonra sırayla dener: Approved→dur; Issuer/Fraud→dur; Technical/Config→sıradaki aday; liste biterse son technical.
- **Rationale**: "Kullanıcı mı tetikledi, süreç mi?" testi → "Öde" senkron kullanıcı-tetikli ⇒ `Process/` değil, `Saga/` değil. Konvansiyon domain-service'e aggregate kökünde izin verir.
- **Alternatives considered**: Ayrı Features slice → tek çağıranı var, sahte niyet yüzeyi. Process/ → bu versiyonda dayanıklı süreç yok.
- **Çift-çekim seam (belgeli, ARTIK CANLI)**: 3 banka canlı → Technical failure'da failover FİİLEN tetiklenir; 1. banka çekmiş olabilir, durum-sorgu YOK → çift-çekim penceresi açık. Simülatörde bakiye/para YOK → şu an zararsız. GERÇEK banka öncesi durum-sorgu ZORUNLU (sonraki versiyon). `<summary>` + FLOW.md'de not.

## D5 — Store sonuç webhook: 041 StoreCallbackDelivery deseninin yeniden kurulumu

- **Decision**: `StoreResultDelivery` (`Deliver` record + tekil `DeliverHandler`): HMAC-SHA256 ham JSON gövde, `X-Signature` lowercase hex, POST → non-2xx throw → Wolverine durable local-queue retry. `CallbackSecret` `StoreCallbackOptions`'tan (secret-store). `PayPaymentLink` `[Transactional]` içinde `bus.PublishAsync` ile enqueue + `PaymentCompleted/Failed` publish (atomik outbox).
- **Rationale**: git `dccf97b` eski `StoreCallbackDelivery.cs`/`CompleteHostedPayment.cs` birebir bu deseni kullanıyordu; altyapı mevcut (`UseDurableLocalQueues`, Marten+Wolverine outbox, fanout exchange'ler Program.cs'te). İlke VI: ham gövde hem imzalanır hem POST edilir, store aynı body'yi doğrular.
- **Alternatives considered**: Senkron HTTP (retry yok) → geçici hata = kayıp bildirim. Yalnız integration event → store HTTP webhook bekliyor (kontrat simetrisi).

## D6 — Kart verisi transient akışı (İlke VI)

- **Decision**: `/pay` body (kart alanları) → `PayPaymentLinkCommand(OpenToken, CardData)` → `BankChargeRequest` → adapter wire. Hiçbir aşamada saklanmaz/loglanmaz. Command `IMessageBus.InvokeAsync` ile çağrılır (ASLA `PublishAsync` — PAN dayanıklı outbox'a düşmesin). Request logging kart gövdesini atlar.
- **Rationale**: İlke VI NON-NEGOTIABLE. SAQ A-EP hedefi (NonSecure'da PG kartı bankaya iletir; vault yok).
- **Alternatives considered**: Kartı aggregate'e/attempt'e koymak → İlke VI ihlali. Command'ı publish etmek → PAN outbox'a yazılır, ihlal.

## D7 — Simülatör test-veri (hesap/bakiye yok), 3 banka

- **Decision**: 3 `SimulatorBankAdapter` örneği (banka başına `SimulatorBankOptions` + farklı `BankCode`), sihirli test-veriyle (kart/tutar → canned sonuç) onay/Issuer/Technical/Fraud üretir; sentinel'ler Options'ta (literal yok). Hesap/bakiye modeli YOK. Tek adapter sınıfı, config'le 3 kez parametrize (ya da named registration).
- **Rationale**: Kullanıcı kararı (3 banka). Ayrı simülatör repo'su henüz yok, "önce PG iskeleti". iyzico sandbox bin+price deseni ([[reference_iyzico_sandbox_test_cards]]) aynası.
- **Alternatives considered**: Zengin Account+balance simülatör → sonraki versiyon (ayrı repo). Rastgele sonuç → test deterministik olmaz. 3 ayrı adapter sınıfı → gereksiz tekrar, config'le parametrize yeter (gerçek bankalar geldiğinde ayrışır).

## D9 — Akıllı routing: 2-aşama (yetenek eleme + on-us/maliyet)

- **Decision**: Router aday listesini iki aşamada kurar: (1) **yetenek eleme** — `PosAccountOptions` içinde bu kart tipini/işlemi desteklemeyen acquirer'ları çıkar; (2) **sıralama** — BinCard'dan çözülen issuer bankası == acquirer bankası ise on-us (en öne), kalanlar maliyet/öncelik sırasına. Failover bu sıralı aday listesini izler. Bilinmeyen BIN → on-us atlanır, maliyet sırası (fail-open).
- **Rationale**: Anayasa İlke VII "routing = yetenek eleme + fiyat; on-us sıralaması". pos-master gateway başına yetenek farkı bu ayrımı doğrular. Saf hesap → domain-TDD'ye uygun (test-first, dış çağrısız).
- **Alternatives considered**: Düz sıralı (OrderedBankCodes) → kullanıcı akıllı routing istedi. Tam fiyat matrisi/circuit-breaker → seam, sonraki versiyon.

## D10 — BinCard: BC-içi salt-okur referans doküman, statik seed

- **Decision**: `BinCard` (BIN→issuer banka + şema + kart tipi) `Domains/BinCards/` altında düz Marten dokümanı; startup'ta embedded JSON'dan seed edilir (küçük sim seti). Güncelleme = reseed (dev). Lookup servisi BIN prefix'iyle çözer, bulunamazsa null (fail-open).
- **Rationale**: Kullanıcı kilidi "BinCard şimdilik statik seed". Eski 008 deseni (embedded JSON, ResolveBin→null). conventions.md: fanout-beslenen salt-okur referans Domains'te kalabilir; aggregate davranışı yok → düz doküman. Dev-faz defansif migration yok ([[feedback_dev_phase_no_defensive_migrations]]).
- **Alternatives considered**: Canlı BIN besleme (Reference-data BC event-only) → sonraki versiyon. BinCard'ı hiç getirmeme → akıllı routing on-us'u imkânsız kılar.

## D8 — Yetki (geçici)

- **Decision**: `/payment-links` (create) + cancel = `payment.write`; müşteri açılış/`/pay` = anonim + gizli OpenToken (fail-closed). Değişmez (089 devamı).
- **Rationale**: İlke V makine düzlemi kararlı; platform IdP (store→gateway) ayrı otorite = sonraki versiyon (constitution TODO(STORE_CONTRACT)). Bu dilim auth modelini değiştirmez.
- **Alternatives considered**: Platform IdP'yi şimdi bağlamak → kapsam dışı, ayrı tasarım oturumu.
