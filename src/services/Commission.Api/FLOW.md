# Commission.Api — Domain Süreci

**BC ne yapar:** Her merchant için gateway'in iyzico maliyeti üstüne uygulayacağı **tutar-kademeli
marj tarifesini** tanımlar. Politika yaşam döngüsünün (oluştur/marj güncelle/statü) yönetim yüzeyi
MCP'dir (044). Kendi DB'sinde yalnız tarife (politika) tutar; ödeme/işlem verisine dokunmaz —
iyzico maliyeti hesaba dışarıdan GİRDİ olarak gelir.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Operatör (Claude Desktop, `external-admin-agent`) merchant için marj politikasını doğal
   dille tanımlar** (044). Tutar-kademeli tarife girilir (her kademe: alt sınır + oran + sabit
   ücret; ilk kademe 0'dan, son kademe açık uçlu); tarife bütünlüğü + kademe başına oran/ücret
   tavanı doğrulanır; merchant başına EN FAZLA bir aktif politika kuralı handler-sorgusuyla
   uygulanır (aggregate cross-aggregate görmez).
   `(CommissionPolicy.Create ← AdminCreateCommissionPolicyMcpTool "admin_create_commission_policy")`
2. **Operatör tarifeyi bütün olarak değiştirir** — TAM kademe seti gönderilir (kısmi patch yok);
   yeni tablo doğrulanır, hatada mevcut tarife DEĞİŞMEZ; değişiklik ileriye dönüktür.
   `(CommissionPolicy.UpdateMargin ← AdminUpdateCommissionMarginMcpTool "admin_update_commission_margin")`
3. **Operatör politikayı aktif/pasif yapar.** Aynı statüye geçiş idempotent no-op; pasif politika
   hesaplamada yok sayılır.
   `(CommissionPolicy.ChangeStatus ← AdminChangeCommissionStatusMcpTool "admin_change_commission_status")`
4. **Operatör politikayı doğal dille sorgular** (043 — salt-okuma özet). Politika yoksa hata
   DEĞİL, "tanımlı politika yok" bilgi döner.
   `(AdminGetCommissionPolicyMcpTool "admin_get_commission_policy")`
5. **Yazma tool'ları `commission.write` scope'una kapılıdır** (044 — Wolverine middleware,
   fail-closed; `/mcp` mount policy'si `commission.read`, yükseltilmedi).
   `(ScopeAuthorizationMiddleware.Before)`
6. **Merchant kendi politikasını görüntüler** — kalan TEK REST ucu; yalnız kendi `{merchantId}`
   (route-claim eşleşmesi, fail-closed). `(GetCommissionPolicyQueryHandler)`

## Domain kuralları (süreci yöneten değişmezler)

- **Tekil-aktif kural.** Merchant başına aynı anda en fazla bir `Active` politika olabilir; ikinci
  oluşturma denemesi reddedilir (aggregate göremediği için handler-lookup ile uygulanır).
- **Bracket kademe seçimi (dilimli değil).** İşlem tutarının düştüğü TEK kademenin oran+sabit ücreti
  TÜM tutara uygulanır (`MarginTariff.ResolveTier`, `CommissionPolicy.CalculateEffectiveCommission`
  aggregate'te YAŞAMAYA DEVAM eder — 044'te yalnız tüketicisiz REST slice'ı silindi).
- **Pasif politika = hesaplama yok.** Aktif politika bulunamazsa hesap yapılmaz; sessiz sıfır
  üretilmez.
- **Tutarsızlık reddi.** Efektif komisyon ödenen tutarı aşarsa (negatif net hakediş) hesap reddedilir.
- **İleriye dönük güncelleme.** Tarife değişikliği geçmiş hesapları etkilemez (`EffectiveCommission`
  value object, store edilmez).
- **Yuvarlama determinizmi.** Gateway marjı 2 ondalığa (kuruş), `AwayFromZero` ile yuvarlanır.

## Sınır (bu BC'nin dokunmadığı)

iyzico'ya canlı çağrı YAPMAZ — maliyet verisi (`ProviderCommission`/`ProviderFee`) her zaman çağıran
tarafından girdi olarak taşınır. Ödeme/tahsilat akışı, merchant onboarding/aktivasyon, komisyon
grid'inin merchant-aktivasyon koşuluna bağlanması (`MerchantCommissionGridReady`, ayrı akış) bu
BC'nin dışıdır. Admin REST uçları (create/margin/status/liste) + `CalculateEffectiveCommission`
REST slice'ı 044'te SÖKÜLDÜ (R2 — hesaplama davranışı aggregate'te durur, gerçek tüketici çıkınca
yeniden açılır); Admin UI komisyon ekranı da YOK — yönetim yalnız MCP.

> **Tasarım borcu — henüz gerçek para akışına bağlı değil.** "Her ödemede otomatik efektif komisyon
> hesapla ve kaydet" süreci henüz KURULMADI; `CommissionPolicy.CalculateEffectiveCommission` bugün
> çağıransız davranıştır (bkz `specs/024-commission-cost-margin`, `specs/030-tiered-commission`).