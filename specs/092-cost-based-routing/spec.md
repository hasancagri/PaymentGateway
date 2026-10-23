> **ABSORBE EDİLDİ → 093 (2026-10-23).** Maliyet-bazlı routing, taksitli ödeme feature'ının (`093-installment-payment`)
> içine alındı: routing artık per-taksit + kart-ailesi efektif maliyet üzerinden (`BankCommissionRate` read-model),
> on-us yalnız eşit maliyette tiebreaker. Bu spec ayrı implement EDİLMEZ; 092 kodu 093'te tamamlandı. Tarihsel.

# Feature Specification: PG Maliyet-Bazlı Routing

**Feature Branch**: `pivot/bank-adapter-skeleton`

**Created**: 2026-10-23

**Status**: Draft

**Input**: User description: "092 PG maliyet-bazlı routing — statik CostPriority yerine efektif MDR hesabı; on-us otomatik-öncelik kalkar, yalnız eşit maliyette tiebreaker; banka otorite (öngörü kopyası, İlke VII). Yeni aggregate/tablo/event YOK."

## Özet

Bugün PG ödeme çekiminde aday bankaları **statik bir öncelik sayısına** (`CostPriority`) göre sıralıyor ve
on-us bankayı (kart issuer'ı = acquirer) her zaman öne alıyor. Bu feature bunu **hesaplanan efektif maliyete**
çevirir: her aday için komisyon oranı × tutar (taksit sayısına göre) hesaplanır ve adaylar **en düşük
maliyetten** sıralanır. On-us otomatik önceliği kalkar — on-us yalnız eşit maliyette belirleyici olur. Oran
matrisi PG'de bir **öngörü kopyasıdır**; banka otoritedir (İlke VII), gerçek maliyet mutabakatta doğrulanır.
Yeni aggregate/tablo/event yok; yalnız routing kararı + öngörü oran verisi değişir.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - En düşük maliyetli uygun bankadan başla (Priority: P1)

Bir ödeme çekiminde birden çok uygun banka varsa, PG her aday için efektif maliyeti hesaplar ve **en düşük
maliyetli** uygun bankadan çekimi başlatır; red/failover olursa bir sonraki en düşük maliyetliye geçer.

**Why this priority**: Feature'ın asıl değeri — doğru (en ucuz) bankaya yönlendirme. Tek başına MVP.

**Independent Test**: Farklı öngörü oranlarına sahip 2+ uygun banka verildiğinde, aday sırasının efektif
maliyet artan düzende olduğu ve çekimin en düşük maliyetliden başladığı doğrulanır (saf routing kararı).

**Acceptance Scenarios**:

1. **Given** MAVIBANK oranı < SARIBANK oranı ve ikisi de uygun, **When** aday sırası hesaplanır, **Then** MAVIBANK ilk sırada (daha düşük efektif maliyet).
2. **Given** ilk (en düşük) banka technical/config reddeder, **When** failover çalışır, **Then** bir sonraki en düşük maliyetli uygun bankaya geçilir.
3. **Given** bir aday bu kart tipini/işlemi desteklemiyor, **When** sıralama yapılır, **Then** o banka aday OLMAZ (maliyeti hesaba katılmaz).

---

### User Story 2 - On-us yalnız eşit maliyette belirleyici (Priority: P2)

On-us bankanın (issuer = acquirer) otomatik önceliği kalkar. İki aday **eşit efektif maliyette** ise ve biri
on-us ise, on-us olan öne gelir; değilse maliyet kararı değiştirmez.

**Why this priority**: Önceki davranışın bilinçli değişimi; eşitlik tiebreaker'ı kararlılık için gerekir ama
çekirdek değer US1'dedir.

**Independent Test**: İki aday eşit maliyette, biri on-us → on-us önce; biri daha ucuz ama on-us değil →
ucuz olan önce (on-us'luk sırayı değiştirmez).

**Acceptance Scenarios**:

1. **Given** iki banka eşit efektif maliyette, biri on-us, **When** sıralanır, **Then** on-us olan önce.
2. **Given** on-us banka daha PAHALI, diğer banka daha ucuz, **When** sıralanır, **Then** ucuz (on-us olmayan) banka önce — on-us otomatik öncelik YOK.
3. **Given** aynı girdi iki kez verilir, **When** sıralanır, **Then** sıra birebir aynı (deterministik).

---

### Edge Cases

- Kart BIN'i bilinmiyor (issuer çözülemez): on-us hesaplanamaz → yalnız maliyet sırası (fail-open, çekim engellenmez).
- Bir banka için öngörü oranı tanımlı değil: güvenli varsayılan oran kullanılır (banka aday listesinden sessizce DÜŞMEZ).
- Tüm adaylar eşit maliyette ve hiçbiri on-us değil: kararlı ikincil sıra (deterministik) uygulanır.
- Tek uygun banka: sıralama anlamsız, o bankadan gidilir.
- Taksit sayısına göre oran değişir: efektif maliyet o taksitin oranıyla hesaplanır (bugünkü akış tek çekim).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem her aday banka için efektif maliyeti öngörü komisyon oranı × işlem tutarı olarak (taksit sayısına uygun oranla) hesaplaMALI.
- **FR-002**: Sistem adayları efektif maliyet ARTAN sırayla sıralaMALI; çekim ve failover bu sırayı izlemeli (en düşük maliyetli önce).
- **FR-003**: Sistem on-us (issuer = acquirer) otomatik önceliğini KALDIRMALI; on-us yalnız eşit efektif maliyette tiebreaker olmalı.
- **FR-004**: Sistem yetenek elemesini (kart tipi + işlem desteği + canlı banka erişimi) maliyet sıralamasından ÖNCE uygulaMALI; uygunsuz banka aday olmamalı.
- **FR-005**: Sistem öngörü oran matrisini banka + taksit bazlı tutMALI; bir banka için oran tanımlı değilse güvenli varsayılan oran kullanmalı (banka adaylıktan düşmemeli).
- **FR-006**: Sistem eşit efektif maliyet + eşit on-us durumunda deterministik, tekrarlanabilir bir ikincil sıra uygulaMALI (aynı girdi → aynı sıra).
- **FR-007**: Sistem bilinmeyen BIN'de (issuer çözülemez) on-us'u atlayıp yalnız maliyet sırasına göre gitMELİ (fail-open, çekim engellenmez).
- **FR-008**: Routing öngörüye dayanMALI; oran matrisi banka otoritesinin PG kopyasıdır (İlke VII); gerçek maliyet mutabakat raporunda doğrulanır (bayatlık orada yakalanır).

### Key Entities *(include if feature involves data)*

- **Acquirer Öngörü (PosAccount)**: Bir bankanın PG'deki yönlendirme öngörüsü — banka kodu + yetenek (kart tipi/işlem) + **banka+taksit bazlı komisyon oran matrisi** + varsayılan oran. Statik `CostPriority` bu matrisle değişir.
- **Routing Adayı**: Bir çekim için hesaplanan aday — banka kodu + **efektif maliyet** + on-us bayrağı. Sıralama anahtarı maliyet (sonra on-us, sonra kararlı fallback).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Birden çok uygun banka varken PG, adayların %100'ünde en düşük efektif maliyetli uygun bankadan başlar.
- **SC-002**: Aynı girdi (kart/tutar/oranlar) için aday sırası %100 tekrarlanabilir (deterministik).
- **SC-003**: Yetenek-dışı (kart tipi/işlem desteklemeyen) banka hiçbir koşulda aday listesinde yer almaz.
- **SC-004**: On-us banka, yalnız eşit maliyette öne gelir; daha pahalı on-us banka, daha ucuz olanın önüne GEÇMEZ (%100).

## Assumptions

- **Komisyon modeli:** PG store'dan marj kesmez (sıfır-marj, mevcut karar); buradaki "maliyet" = bankanın PG'ye uygulayacağı MDR öngörüsü, routing sinyali olarak kullanılır.
- **Oran kaynağı:** Öngörü oran matrisi non-secret config'te (banka otorite; gerçek oran bankanın `Merchant.CommissionRates`'inde, PG kopyası bayatlayabilir — İlke VII).
- **On-us kararı (my-call, onayında değişebilir):** on-us otomatik öncelik KALDIRILIR, yalnız eşit maliyette tiebreaker (kullanıcı "hesaplama olacak" dedi; saf maliyet-öncelik yorumu).
- **Taksit:** Bugünkü akış tek çekim; matris taksit-bazlı tanımlı ama bu dilimde tek-çekim oranı kullanılır.
- **Kapsam dışı:** Gerçek-zamanlı banka oran sorgusu, dinamik oran güncelleme, komisyon tahsili/hesabı — hepsi sonraki dilim; bu dilim yalnız öngörü-tabanlı routing sıralaması.
- **Değişmez:** Yeni aggregate/tablo/event/kontrat YOK; yalnız yönlendirme domain-servisi kararı + öngörü oran verisi + testler.
