# Özellik Spesifikasyonu: Merchant Self-Servis MerchantKey Yenileme (Reissue)

**Özellik Dalı**: `046-merchant-key-reissue`

**Oluşturulma**: 2026-09-20

**Durum**: Taslak

**Girdi**: Kullanıcı tanımı: "Merchant self-servis MerchantKey yenileme/rotate (G8) — MerchantKey'ini
unutan/kaybeden ya da sızdığından şüphelenen merchant kendi kendine yeni key alabilsin. Tetik
mağaza-tarafı, kimlik-bazlı (MerchantId, kaybolan key değil), eski key anında ölür, yeni key tek-
kullanımlık reveal ekranından elle teslim edilir."

## Kullanıcı Senaryoları & Test *(zorunlu)*

### Kullanıcı Hikâyesi 1 - Kaybolan/unutulan key'i yenileme (Öncelik: P1)

Bir merchant (gateway'in müşterisi olan mağaza) MerchantKey'ini artık bulamıyor — kaydetmemiş,
kaybetmiş ya da sızdığından şüpheleniyor. Merchant kendi admin yüzeyinden yeni key ister; kendini
**sahip olmadığı key ile değil**, kimliğiyle (merchant kimliği) tanıtır. Gateway yepyeni bir key
üretir, yeni key üretildiği an eski key çalışmayı bırakır, merchant yeni key'i tek-kullanımlık reveal
ekranından **bir kez** okuyup kendi sistemine girer.

**Neden bu öncelik**: Özelliğin tüm amacı bu. Bu olmadan key'i kaybolan merchant kilitli kalır ve
operatör tarafından baştan onboarding'e alınması gerekir. Tek başına gösterilebilir MVP.

**Bağımsız Test**: Bilinen bir merchant kimliği için yenileme tetikle; yeni key üretildiğini, önceki
key'in ödeme yüzeyinde anında reddedildiğini (kimlik doğrulama başarısız), yeni key'in başarıyla
doğrulandığını teyit et.

**Kabul Senaryoları**:

1. **Verildiğinde** çalışan key'i olan aktif bir merchant, **yapıldığında** merchant kendini merchant
   kimliğiyle tanıtıp yenileme isteğinde bulunur, **o zaman** yeni bir key üretilir ve tek-kullanımlık
   reveal ile sunulur; istek eski key'i GEREKTİRMEZ.
2. **Verildiğinde** yeni tamamlanmış bir yenileme, **yapıldığında** herhangi bir istek *eski* key ile
   doğrulanmaya çalışır, **o zaman** reddedilir (kimlik doğrulama başarısız) — grace period YOK.
3. **Verildiğinde** yeni tamamlanmış bir yenileme, **yapıldığında** bir istek *yeni* key ile
   doğrulanır, **o zaman** başarılı olur.
4. **Verildiğinde** yenilemeden önce zaten başlamış bir ödeme (hosted ödeme linki zaten üretilmiş),
   **yapıldığında** müşteri o ödemeyi tamamlar, **o zaman** ödeme yine başarılı olur (zaten üretilmiş
   link ve sonuç bildirimi merchant key'e bağlı değil).

---

### Kullanıcı Hikâyesi 2 - İzlenebilir yenileme geçmişi (Öncelik: P2)

Bir operatör ya da merchant, key'in yenilendiğini, ne zaman, kim tarafından ve opsiyonel olarak neden
yapıldığını sonradan görebilir. Bu iki tarafı da korur: merchant "benim key'imi habersiz
değiştirdiniz" diyemez, operatör şüpheli rotate'leri araştırabilir.

**Neden bu öncelik**: Credential değiştiren bir işlem için hesap-verebilirlik gerekir, ama çekirdek
yenileme (P1) kendi başına değer üretir. Bu, üstüne güvence katmanı ekler.

**Bağımsız Test**: Bir yenileme yap, sonra o merchant'ın yenileme geçmişini oku; zaman damgası,
tetikleyen aktör ve opsiyonel neden içeren bir kayıt bulunduğunu teyit et.

**Kabul Senaryoları**:

1. **Verildiğinde** tamamlanmış bir yenileme, **yapıldığında** merchant'ın yenileme geçmişi okunur,
   **o zaman** zaman, tetikleyen aktör ve varsa neden notunu içeren bir kayıt görünür.
2. **Verildiğinde** zaman içinde birden çok yenileme, **yapıldığında** geçmiş okunur, **o zaman** tüm
   kayıtlar sırayla mevcuttur ve hiçbiri düzenlenemez/silinemez (salt-append).

---

### Kullanıcı Hikâyesi 3 - Merchant'ı çıplak kimlik yerine adla belirtme (Öncelik: P3)

Yenilemeyi başlatan kişi merchant'ı adıyla bilir, iç opak kimlikle değil. Merchant adını (ya da
e-postasını) verir; sistem bunu merchant kimliğine çözer ve devam eder. Böylece opak kimlik elle
yazımdan ve sohbetten uzak kalır.

**Neden bu öncelik**: Ergonomik. Yenileme çıplak kimlikle çalışır (P1); ad çözümlemesi sürtünmeyi
azaltır ve ayrı bir merchant-lookup yeteneğiyle eşleşir ama çekirdek akış için şart değil.

**Bağımsız Test**: Tam olarak bir merchant'a uyan bir ad ver; yenilemenin doğru merchant'ı
hedeflediğini teyit et. Belirsiz ya da bilinmeyen bir ad ver; net bir hata ve hiçbir key değişikliği
olmadığını teyit et.

**Kabul Senaryoları**:

1. **Verildiğinde** tam bir merchant'a uyan bir ad, **yapıldığında** adla yenileme istenir, **o
   zaman** doğru merchant'ın key'i yenilenir.
2. **Verildiğinde** sıfır ya da birden fazla merchant'a uyan bir ad, **yapıldığında** adla yenileme
   istenir, **o zaman** istek belirsizlik/bulunamadı sonucuyla reddedilir ve hiçbir key değişmez.

---

### Sınır Durumları

- **Var olmayan ya da aktif olmayan merchant için yenileme**: istek reddedilir; hiçbir şey değişmez.
- **Aynı merchant için eşzamanlı yenileme istekleri**: yalnız bir yeni key geçerli olur; sistem iki
  farklı "yeni" key'in ikisinin de geçerli sayıldığı bir duruma DÜŞMEZ.
- **Yeni key hiç reveal edilmezse**: merchant tek-kullanımlık reveal'ı hiç açmasa bile eski key yine
  ölüdür (anında geçersizleme, reveal'ın açılmasına bağlı değil). Okunabilir bir key almak için
  merchant tekrar yenileme yapmalı. Bu bilinçli (anında-öldür tek kuraldır).
- **Reveal birden fazla kez / süre dolduktan sonra açılırsa**: key en fazla bir kez gösterilir ve
  reveal süresi dolar; ikinci deneme hiçbir şey göstermez, yeni yenileme gerektirir.
- **Yenileme aralığında eski key ile yeni-ödeme başlatma denemesi**: artık ölü olan eski key ile
  denenen yeni ödeme başlatma başarısız olur; merchant yeni key'i rebind edince müşteri tekrar dener.
  Para hareket etmediği için mali etki yoktur.
- **Reveal'ı henüz tüketilmemiş bir önceki yenilemenin hemen ardından yeni yenileme**: en son yenileme
  kazanır; önceki reveal edilmemiş key de geçersizdir.

## Gereksinimler *(zorunlu)*

### Fonksiyonel Gereksinimler

- **FR-001**: Sistem, merchant'ın mevcut MerchantKey'i sunmadan yeni bir MerchantKey almasına izin
  VERMELİDİR. İstek, merchant kimliği + güvenilir tetik kanalıyla yetkilendirilmeli; ASLA değiştirilen
  key ile değil.
- **FR-002**: Yenilemede sistem, onboarding'de üretilen key ile aynı güç/format garantilerine sahip
  yeni bir key ÜRETMELİDİR.
- **FR-003**: Yenilemede önceki key ANINDA geçersiz olmalı — grace period YOK, eski ve yeni key'in
  ikisinin birden doğrulandığı hiçbir pencere OLMAMALIDIR.
- **FR-004**: Anında geçersizleme, merchant key'in kimlik doğrulamada kullanılan **HER** temsiline
  uygulanmalı — doğrudan API-key yolu (ödeme yüzeyi) + merchant OAuth client secret (kimlik/token
  üretimi) dahil. Yenileme sonrası eski key'in hiçbir temsili kullanılabilir OLMAMALIDIR.
- **FR-005**: Yeni key, elle tek-kullanımlık reveal ile teslim edilmeli — merchant bir kez okuyup
  kendi sistemine girer. Key sohbet/AI kanalından, komut çıktısından ya da loglardan GEÇMEMELİDİR.
- **FR-006**: Reveal tek-kullanımlık ve süreli OLMALIDIR; tüketildikten ya da süresi dolduktan sonra
  key'i tekrar AÇMAMALIDIR.
- **FR-007**: Sistem her yenilemeyi salt-append bir denetim kaydı olarak TUTMALIDIR: en az merchant,
  zaman, tetikleyen aktör ve opsiyonel neden notu. Denetim kayıtları düzenlenemez/silinemez OLMALIDIR.
- **FR-008**: Yenileme nedeni sistem davranışını DEĞİŞTİRMEMELİDİR — "unuttum", "kaybettim", "sızma
  şüphesi" hepsi aynı anında-geçersizlemeyle sonuçlanır. Neden yalnız denetim metadata'sıdır.
- **FR-009**: Hedef merchant yoksa, işlem yapamayacak bir durumdaysa ya da (adla istekte) tam olarak
  bir merchant'a çözülemiyorsa yenileme isteği HİÇBİR key değişikliği yapmadan reddedilmelidir.
- **FR-010**: Eşzamanlı ya da tekrarlı yenileme istekleri tek bir geçerli mevcut key'de BİRLEŞMELİDİR;
  sistem birden fazla key'i aynı anda geçerli BIRAKMAMALIDIR.
- **FR-011**: Sistem, yenileme tetiği için merchant'ı ad ya da e-postayla kimliğine çözmeyi
  DESTEKLEMELİDİR (belirsiz/bilinmeyen girdiyi reddederek). (Ayrı merchant-lookup yeteneğiyle eşleşir;
  FR-001'deki çıplak-kimlik yolu tek başına yeterlidir.)
- **FR-012**: Yenileme tetiği kötüye-kullanım kontrollerine (oran/deneme sınırı) TABİ OLMALIDIR;
  merchant'ın key'ini churn etmek ya da bir engelleme (denial) vektörü olarak kullanılamamalı.

### Repo-Arası Entegrasyon (bağlam, bu spec kapsamı dışı)

Merchant-yüzü tetik ve yeni key'in merchant'ın kendi sistemine yeniden girilmesi/rebind'i mağaza
tarafında yaşar (bir tetik aksiyonu + mevcut tek-kullanımlık credential-giriş ekranı ve key'i merchant
bilgisine bind etmesi). Bu gateway-kapsamlı spec, yenileme otoritesini kapsar: yeni-key üretimi, tüm
temsillerde anında eski-key geçersizleme, tek-kullanımlık reveal ve denetim. Mağaza-tarafı tetik +
rebind ince tamamlayıcıdır, ayrı izlenir.

### Ana Varlıklar *(veri içeriyorsa)*

- **Merchant**: Key'i yenilenen gateway müşterisi. İç kimlikle tanımlı (ad/e-postayla çözülebilir).
  Aynı anda tam olarak bir geçerli key'e sahiptir.
- **MerchantKey (credential)**: Merchant'ın gateway'e kimlik kanıtladığı sır. Birden çok kimlik-
  doğrulama temsilinde var olur (API-key referansı + OAuth client secret); yenileme merchant açısından
  hepsini atomik değiştirir.
- **Tek-Kullanımlık Reveal**: Yeni üretilen key'in tek-kullanımlık, süreli açığa çıkarılması; bir insan
  tam olarak bir kez okur.
- **Yenileme Denetim Kaydı**: Bir yenilemenin salt-append kaydı: merchant, zaman damgası, tetikleyen
  aktör, opsiyonel neden.

## Başarı Kriterleri *(zorunlu)*

### Ölçülebilir Sonuçlar

- **SC-001**: Key'ini kaybeden bir merchant, operatör müdahalesi olmadan, uçtan uca 5 dakikanın
  altında çalışan yeni bir key alabilir.
- **SC-002**: Yenilemeden hemen sonra eski key ile yapılan kimlik doğrulama denemelerinin %100'ü
  reddedilir; yeni key ile doğrulama ilk denemede başarılı olur.
- **SC-003**: Yenileme anında zaten süren bir ödeme (link zaten üretilmiş) vakaların %100'ünde
  başarıyla tamamlanır — yenileme sıfır mali kayba yol açar.
- **SC-004**: Her yenileme tam olarak bir salt-append denetim kaydı üretir; kayıtsız yenileme olmaz.
- **SC-005**: Yeni key hiçbir sohbet transkriptinde, komut çıktısında ya da logda bulunmaz — yalnız
  tek-kullanımlık reveal yüzeyinde görünür.
- **SC-006**: Yenileme istekleri asla eski key'i gerektirmez; key'i tamamen kayıp bir merchant akışı
  %100 tamamlayabilir.

## Varsayımlar

- Tetik kanalı, merchant'ın kendi admin yüzeyinin gateway'e yaptığı bir S2S istektir; mağazanın
  mevcut makine credential'ıyla yetkilenir (merchant key ile değil). Merchant kimliği (ya da
  çözülebilir ad/e-posta) istekte taşınır.
- Merchant'lar pratikte "aktif" durumdadır (eski kademeli-provisioning statü zinciri işletilmez);
  yenileme zaten onboarding'i tamamlanmış, işlem yapan bir merchant'ı hedefler.
- Tek-kullanımlık reveal, onboarding'de kullanılan mevcut tek-kullanımlık key-reveal mekanizmasını
  yeniden kullanır; gateway tarafında yeni insan-yüzü ekran eklenmez.
- Merchant ad/e-posta çözümlemesi ayrı izlenen bir merchant-lookup yeteneğine bağlıdır; o gelene dek
  çıplak-kimlik yolu (FR-001) desteklenen tetiktir.
- Sonuç-bildirimi (callback) yolu etkilenmez; çünkü merchant key ile değil, ayrı bir sırla korunur.
- Kötüye-kullanım kontrolleri (FR-012) platformun ayrı izlenen genel rate-limiting sertleştirmesinin
  üstüne kurulur; bu özellik yalnız yenileme ucunun onunla kapsanmasını gerektirir.