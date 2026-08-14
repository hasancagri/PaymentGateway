# Feature Specification: iyzico Saklı Kart'a Geçiş (Model A)

**Feature Branch**: `032-iyzico-card-storage`

**Created**: 2026-08-14

**Status**: Draft

**Input**: User description: "Model A kart saklama — iyzico vault'a geçiş: 031'de kurulan kendi vault'umuz (Model B, AES-korumalı PAN) iyzico'nun Saklı Kart modeline geçirilir. Amaç: CVC-siz tekrar ödeme + ileride recurring için gerekli iyzico cardUserKey/cardToken altyapısı. Dış sözleşme korunur (sıfır ECommerce dokunuş). iyzico çağrısı artık VAR (Model B kararı bilinçli tersine döner)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Kart Kaydetme (iyzico Saklı Kart) (Priority: P1)

Son kullanıcı ECommerce profilinden kart bilgisini girer (numara, son kullanma, sahip adı; güvenlik
kodu gateway'e gitmez). ECommerce, kartı merchant kimliğiyle gateway'e iletir. Gateway kartı KENDİSİ
saklamaz; ödeme sağlayıcısının (iyzico) Saklı Kart servisine iletir. Sağlayıcı kartı kendi güvenli
kasasında saklar ve iki kimlik döndürür: kullanıcı-kimliği (aynı kullanıcının sonraki kartları buna
eklenir) ve kart-kimliği. Gateway yalnız bu iki kimliği + gösterim bilgilerini (marka, son 4 hane,
ilk 6 hane) saklar; kart numarası gateway'de HİÇ durmaz. Karşılığında ECommerce'e tek opak token
döner (mevcut sözleşme birebir korunur).

**Why this priority**: Model A'nın çekirdeği. CVC-siz tekrar ödeme ve recurring ancak sağlayıcı
kasasında saklı kartla mümkün; bu story o altyapıyı kurar. 031'in kendi-kasa modeli bunun yerini alır.

**Independent Test**: Merchant token'ıyla gateway'e geçerli kart gönderilir; sağlayıcıda kart oluşur,
gateway kaydında sağlayıcı kimlikleri + gösterim alanları bulunur, kart numarası hiçbir yerde açık
değildir; ECommerce'e yalnız opak token döner. ECommerce profilinden uçtan uca: kart ekle → listede
marka+son4.

**Acceptance Scenarios**:

1. **Given** Aktif merchant token'ı, **When** geçerli kart gönderilir, **Then** sağlayıcıda kart saklanır, gateway kaydında sağlayıcı kullanıcı-kimliği + kart-kimliği + marka/son4/ilk6 bulunur ve ECommerce'e yalnız opak token döner.
2. **Given** sağlayıcının geçersiz bulduğu bir kart (biçim/doğrulama), **When** gönderilir, **Then** gateway kaydı oluşmaz ve alan hatası döner (sağlayıcının reddi kullanıcıya anlaşılır iletilir).
3. **Given** ikinci bir kart gönderilir, **When** işlenir, **Then** sağlayıcıdan yeni bir (kullanıcı-kimliği + kart-kimliği) çifti alınır ve yeni opak token döner (her kart bağımsız çift taşır — bkz. Assumptions: gruplama ertelendi, sözleşme buyer kimliği taşımıyor).
4. **Given** kart saklama isteği, **When** işlenir, **Then** gateway'in kendi veri deposunda ve loglarında açık kart numarası 0 adettir (sağlayıcıda saklanır).
5. **Given** başka merchant'ın kimliğiyle adreslenen uç, **When** merchant kendi token'ıyla çağırır, **Then** erişim reddedilir (kiracı sınırı korunur).

---

### User Story 2 - Kart Silme (Priority: P2)

Son kullanıcı ECommerce profilinden kartını siler. Gateway hem kendi kaydını iptal eder hem de
sağlayıcının kasasındaki kartı siler. Sağlayıcıya ulaşılamazsa yerel iptal yine tamamlanır (mevcut
en-iyi-çaba davranışı korunur); gateway kaydı iptal (tarihçede kalır) olur.

**Why this priority**: Yaşam döngüsü kapanışı; sağlayıcı kasasında yetim kart bırakmamak için silme
iki tarafı da kapsamalı.

**Independent Test**: Var olan token silinir → gateway kaydı iptal + sağlayıcıdan kart kalkar; aynı
token ikinci silme başarılı kabul edilir (idempotent); bilinmeyen/başka-merchant token'ı bulunamadı döner.

**Acceptance Scenarios**:

1. **Given** kayıtlı bir kart token'ı, **When** sahibi merchant siler, **Then** gateway kaydı iptal olur ve sağlayıcının kasasından kart silinir.
2. **Given** iptal edilmiş token, **When** tekrar silinir, **Then** işlem başarılı kabul edilir (idempotent).
3. **Given** var olmayan / başka merchant'a ait token, **When** silinir, **Then** kayıt bulunamadı döner (sahiplik sızdırmaz).
4. **Given** sağlayıcıya ulaşılamıyor, **When** silme yapılır, **Then** yerel iptal yine tamamlanır (en-iyi-çaba; kullanıcı silmesi bloke olmaz).

---

### Edge Cases

- Güvenlik kodu (CVC) sözleşmede yok; gönderilse bile kabul eden alan yok (sağlayıcıya da gateway'den CVC gitmez — saklama için gerekmez).
- Sağlayıcı geçici erişilemezse kart saklama başarısız döner (fail-closed): gateway kaydı oluşmaz, kullanıcıya "şu an eklenemedi" denir (yarım kayıt bırakılmaz).
- Aynı kart ikinci kez saklanırsa sağlayıcıdan yeni çift döner; gateway yeni kayıt açar (mükerrer kullanıcı sorumluluğu; gruplama olmadığından mükerrer engellenmez).
- Pasif/Askıda merchant kart saklama yetkisi taşıyan token alamaz (mevcut statü-kapılı yetki korunur).
- 031'de kendi kasamızda saklanmış eski kartlar bu modele taşınMAZ (dev verisi sıfırlanır; taşıma migration'ı yazılmaz).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, yetkili merchant'tan kart kaydetme isteğini (kart numarası + son kullanma + kart sahibi adı) kabul edip ödeme sağlayıcısının Saklı Kart servisine iletmeli; karşılığında sağlayıcının döndürdüğü kullanıcı-kimliği + kart-kimliğini saklamalı ve ECommerce'e YALNIZ opak bir token döndürmelidir.
- **FR-002**: Kart numarası gateway'de HİÇBİR biçimde (açık veya şifreli) saklanMAZ; kart sağlayıcının kasasında durur. Gateway yalnız sağlayıcı kimliklerini + gösterim alanlarını (marka, son 4, ilk 6) tutar.
- **FR-003**: Güvenlik kodu (CVC) sözleşmenin parçası DEĞİLDİR; sistem kabul etmez, saklamaz, sağlayıcıya saklama amacıyla göndermez.
- **FR-004**: Her saklanan kart, sağlayıcıdan KENDİ (kullanıcı-kimliği + kart-kimliği) çiftini alır ve kendi opak token'ını üretir. Aynı son kullanıcının kartlarını tek kullanıcı-kimliği altında gruplama bu feature kapsamında DEĞİLDİR — dış sözleşme buyer/son-kullanıcı kimliği taşımadığından (sıfır dokunuş, FR-008) gruplama yapılamaz; ödeme için gerekmez, ileride buyer kimliği sözleşmeye girerse eklenir.
- **FR-005**: Her kayıt, kaydı yapan merchant'a bağlıdır (kiracı sınırı): merchant yalnız kendi kimliğiyle adreslenen uçlara erişir, başka merchant'ın token'ı üzerinde işlem yapamaz.
- **FR-006**: Merchant, token ile kaydı silebilmelidir: gateway kaydı iptal olur (tarihçede kalır) VE sağlayıcının kasasındaki kart silinir; sağlayıcıya ulaşılamazsa yerel iptal yine tamamlanır (en-iyi-çaba); iptal edilmiş kaydın tekrar silmesi başarılı kabul edilir (idempotent); bilinmeyen token bulunamadı döner.
- **FR-007**: Kart saklama isteği sağlayıcı tarafından reddedilirse (biçim/doğrulama/erişim) gateway kaydı oluşmaz (fail-closed) ve alan bazlı anlaşılır hata döner; yarım kayıt bırakılmaz.
- **FR-008**: Dış sözleşme (ECommerce'in kullandığı kart ekleme + silme uçları ve token yanıtı) 031 ile birebir aynı kalır; ECommerce tarafında hiçbir kod/config değişikliği gerekmez (sıfır dokunuş).
- **FR-009**: Sağlayıcı erişim kimlik bilgileri (API anahtarları) yapılandırmadan gelir; kaynak koda gömülmez ve sürüm kontrolüne girmez.
- **FR-010**: Kasa uçları mevcut kart-kasası yetki düzlemini kullanır (kart yazma yetkisi yalnız Aktif merchant'ta — mevcut statü-kapılı zincir değişmez).

### Key Entities

- **StoredCard (Kayıtlı Kart) — değişir**: Kimlik = opak token; sahibi merchant; **sağlayıcı kullanıcı-kimliği + sağlayıcı kart-kimliği** (kart numarası yerine); gösterim alanları (marka, son 4, ilk 6); son kullanma; kart sahibi adı; durum (Aktif/İptal). PAN alanı KALDIRILIR.
- **Sağlayıcı Saklı Kart (dış)**: Ödeme sağlayıcısının kasasındaki kart; gateway yalnız kimlikleriyle (kullanıcı-kimliği + kart-kimliği çifti, per-kart) referans verir, içeriğini görmez.
- **Merchant**: Mevcut aggregate — kiracı sınırı + sağlayıcı erişim bağlamı referansı; değişmez.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: ECommerce tarafında SIFIR değişiklikle, son kullanıcı profil ekranından kart ekleme akışı uçtan uca tamamlanır: kart ekle → listede marka+son4; 031 modelinden Model A'ya geçiş kullanıcıya görünmez.
- **SC-002**: Kart saklama sonrası gateway veri deposunda ve loglarında açık kart numarası 0 adettir (kart sağlayıcıda saklanır; gateway'de yalnız sağlayıcı kimlikleri + gösterim alanları).
- **SC-003**: Her kart kaydında sağlayıcıdan geçerli bir (kullanıcı-kimliği + kart-kimliği) çifti alınıp saklanır (%100); bu çift sonraki CVC-siz ödemenin girdisidir. (Aynı-kullanıcı gruplaması bu feature'da yok — bkz. FR-004.)
- **SC-004**: Silme akışı uçtan uca: ECommerce listeden sil → gateway kaydı iptal + sağlayıcı kasasından kart kalkar; tekrar silme hata üretmez.
- **SC-005**: Kart saklama, CVC-siz tekrar ödeme için gerekli sağlayıcı kimliklerini (kullanıcı-kimliği + kart-kimliği) üretir ve saklar (sonraki ödeme akışı feature'ının önkoşulu karşılanır).

## Assumptions

- Kart saklama artık ödeme sağlayıcısına (iyzico) gerçek çağrı yapar; bu, 031'in "sağlayıcıya çağrı yok" (Model B) kararının BİLİNÇLİ tersine çevrilmesidir — gerekçe: CVC-siz tekrar ödeme + recurring yalnız sağlayıcı Saklı Kart'ıyla mümkün.
- Sağlayıcı bağlantısı geliştirme-dönemi sandbox ortamıyla doğrulanır (bağlantı + imza spike ile kanıtlandı); üretim kimlik bilgileri ayrı yapılandırmadır.
- Gösterim alanları (marka, son 4, ilk 6) sağlayıcı yanıtından alınır; sağlayıcı vermezse kart numarasından yerel türetilir (yalnız saklama anında, ham numara saklanmadan).
- 031'de kendi kasamızda saklanmış kartlar taşınmaz; dev verisi sıfırlanır (defansif migration yok).
- Kart-saklama merchant'ın sağlayıcı bağlamıyla ilişkilidir; sub-merchant kaydı (ayrı iş) bu feature'ın önkoşulu DEĞİLDİR — saklama merchant düzeyinde çalışır.
- **Aynı-kullanıcı kart gruplaması ERTELENDİ**: dış sözleşme (031) buyer/son-kullanıcı kimliği taşımadığından (ECommerce sabit "CARD HOLDER" gönderir, e-posta yok), gateway aynı kullanıcıyı ayırt edemez. Her kart bağımsız (kullanıcı-kimliği + kart-kimliği) çifti alır; sağlayıcıya sentetik/sabit e-posta ile kayıt yapılır. Gruplama, ileride buyer kimliği sözleşmeye eklenirse gelir (o zaman mevcut kullanıcı-kimliği sağlayıcıya iletilir). Ödeme (CVC-siz) için gruplama gerekmez.
- Kayıtlı kartla ÖDEME bu feature kapsamında YOK — ödeme akışı ayrı spec; bu feature yalnız sağlayıcı-saklı sakla/sil döngüsünü kurar (ama ödeme için gereken kimlikleri üretir — SC-005).