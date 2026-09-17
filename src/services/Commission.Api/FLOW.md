# Commission.Api — Domain Süreci

**BC ne yapar:** Her merchant için gateway'in iyzico maliyeti üstüne uygulayacağı **tutar-kademeli
marj tarifesini** tanımlar ve verili bir işlem bağlamı için **efektif komisyon + merchant net
hakedişini** hesaplar. Kendi DB'sinde yalnız tarife (politika) tutar; ödeme/işlem verisine dokunmaz
— iyzico maliyeti hesaba dışarıdan GİRDİ olarak gelir.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Yönetici merchant için marj politikası tanımlar.** Merchant başına tutar-kademeli tarife
   girilir (her kademe: alt sınır + oran + sabit ücret; ilk kademe 0'dan başlar, son kademe açık
   uçlu); tarife bütünlüğü + kademe başına oran/ücret tavanı doğrulanır; merchant başına EN FAZLA
   bir aktif politika kuralı handler-sorgusuyla uygulanır (aggregate cross-aggregate görmez)
   `(CommissionPolicy.Create ← CreateCommissionPolicyCommandHandler)`.
2. **Yönetici mevcut politikanın tarifesini bütün olarak değiştirir.** Yeni tablo doğrulanır;
   hatada mevcut tarife DEĞİŞMEZ; değişiklik yalnız ileriye dönüktür (geçmiş hesaplar yeniden
   fiyatlanmaz) `(CommissionPolicy.UpdateMargin ← UpdateCommissionPolicyMarginCommandHandler)`.
3. **Yönetici politikayı aktif/pasif yapar.** Aynı statüye geçiş idempotent no-op sayılır; pasif
   politika hesaplamada yok sayılır `(CommissionPolicy.ChangeStatus ← ChangeCommissionPolicyStatusCommandHandler)`.
4. **Verili bir işlem için efektif komisyon hesaplanır.** Girdi: ödenen tutar, iyzico maliyeti
   (`ProviderCommission`+`ProviderFee`, işlem-sonrası rapordan gelen string alanlar), taksit sayısı.
   Aktif politika merchant için aranır (yoksa "politika yok" reddi — sessiz 0 üretilmez); sağlayıcı
   maliyeti string'ten decimal'e ayrıştırılır (anti-corruption sınır, handler'da); tutarın düştüğü
   TEK kademe (bracket — dilimli/birikimli değil) tüm tutara uygulanarak gateway marjı bulunur;
   efektif komisyon = iyzico maliyeti + gateway marjı; efektif komisyon ödenen tutarı aşarsa
   tutarsızlık olarak reddedilir (negatif hakediş üretilmez); net hakediş = ödenen tutar − efektif
   komisyon `(MarginTariff.ResolveTier, CommissionPolicy.CalculateEffectiveCommission ← CalculateEffectiveCommissionQueryHandler → EffectiveCommission)`.
5. **Merchant kendi politikasını görüntüler.** Yalnız kendi `{merchantId}` (route-claim eşleşmesi,
   fail-closed) `(GetCommissionPolicyQueryHandler)`. Yönetici tüm politikaları merchant/statü
   filtresiyle listeler `(ListCommissionPoliciesQueryHandler)`.

## Domain kuralları (süreci yöneten değişmezler)

- **Tekil-aktif kural.** Merchant başına aynı anda en fazla bir `Active` politika olabilir; ikinci
  oluşturma denemesi reddedilir (aggregate göremediği için handler-lookup ile uygulanır).
- **Bracket kademe seçimi (dilimli değil).** İşlem tutarının düştüğü TEK kademenin oran+sabit ücreti
  TÜM tutara uygulanır — üst kademelere geçince alt kademeler ayrıca hesaba katılmaz.
- **Pasif politika = hesaplama yok.** Aktif politika bulunamazsa (yok veya pasif) hesap yapılmaz;
  sessiz sıfır üretilmez, açık "politika yok" hatası döner.
- **Tutarsızlık reddi.** Efektif komisyon ödenen tutarı aşarsa (negatif net hakediş anlamına gelir)
  hesap reddedilir, kayıt üretilmez.
- **İleriye dönük güncelleme.** Tarife değişikliği geçmiş hesapları etkilemez (Commission hiçbir
  hesap sonucunu kalıcı tutmaz — `EffectiveCommission` value object, ORM'e store edilmez).
- **Yuvarlama determinizmi.** Gateway marjı 2 ondalığa (kuruş), `AwayFromZero` ile yuvarlanır.

## Sınır (bu BC'nin dokunmadığı)

iyzico'ya canlı çağrı YAPMAZ — maliyet verisi (`ProviderCommission`/`ProviderFee`) her zaman çağıran
tarafından girdi olarak taşınır (işlem-sonrası rapor/payout kaynaklı, Commission bunu üretmez/çekmez).
Ödeme/tahsilat akışı, merchant onboarding/aktivasyon, komisyon grid'inin merchant-aktivasyon koşuluna
bağlanması (`MerchantCommissionGridReady`, ayrı akış) bu BC'nin dışıdır.

> **Tasarım borcu — henüz gerçek para akışına bağlı değil.** `PaymentChargedEvent` (Payment.Api
> yayınlar, iyzico maliyetini `CalculateEffectiveCommission` girdi imzasıyla uyumlu taşır) şu an
> HİÇBİR tüketicisi olmayan bir bağlantı noktası; `CalculateEffectiveCommissionQueryHandler` yalnız
> REST üstünden elle/S2S çağrılabilir, charge akışına otomatik bağlı DEĞİL. Yani bu BC bugün yalnız
> bir **tanım + hesaplama** servisidir — "her ödemede otomatik efektif komisyon hesapla ve kaydet"
> süreci henüz KURULMADI (bkz `specs/024-commission-cost-margin`, `specs/030-tiered-commission`).
