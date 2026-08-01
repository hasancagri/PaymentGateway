# Feature Specification: Merchant Key (gateway kimliği)

**Feature Branch**: `006-merchant-key`

**Created**: 2026-08-02

**Status**: Draft

**Input**: User description: "Merchant aggregate'ine merchantKey ekle: her merchant için benzersiz, dış sistemlerin (ödeme akışı) merchant'ı tanımlamak için kullandığı stabil bir anahtar (merchantKey). Onboarding sırasında üretilir/atanır, benzersizdir, değişmez."

## Bağlam ve Amaç

Bu bir payment gateway'dir. Merchant kimliğini **gateway** verir, merchant değil (iyzico/Stripe/PayU
modeli: acquirer/PSP merchant'a kimlik atar). **merchantKey**, gateway'in her merchant'a onboarding'de
mint ettiği benzersiz, stabil, açık (gizli olmayan) dış kimliktir. Merchant'ın iç kimliği (`Id`, Guid)
gateway içinde kalır; merchantKey ise dış sistemlerin (ileride ödeme akışı) merchant'ı isimlendirmek
için kullanacağı değerdir. Bu dilim key'i **üretir ve görünür kılar**; ödeme akışına bağlama gelecekteki
gerekçedir, kapsam dışıdır (aşağıya bakınız).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Onboarding'de gateway merchant'a benzersiz key atar (Priority: P1)

Yeni bir merchant kaydı oluşturulduğunda sistem (gateway) o merchant'a otomatik olarak benzersiz bir
**merchantKey** üretir ve kalıcı olarak kaydeder. Key sunucu tarafında üretilir; çağıran taraf (admin
formu dahil) key sağlayamaz, sağlasa bile yok sayılır. merchantKey oluşturma yanıtında döner ve
merchant'ı sorgulayan her yerde görünür — böylece gateway operatörü onu merchant firmasına iletebilir.

**Why this priority**: Gateway modelinde merchant kimliği key'dir. Key üretimi tüm key-tabanlı
davranışların (arama, ileride ödeme bağlama, teslim) ön koşuludur ve tek başına çalışır bir dilimdir (MVP).

**Independent Test**: Geçerli merchant bilgileriyle oluşturma yapıldığında yanıt boş olmayan benzersiz
bir merchantKey içerir; aynı merchant tekrar sorgulandığında aynı key döner.

**Acceptance Scenarios**:

1. **Given** geçerli merchant bilgileri, **When** merchant oluşturulur, **Then** kayıt oluşur ve yanıt
   benzersiz, boş olmayan bir merchantKey içerir.
2. **Given** oluşturulmuş bir merchant, **When** merchant Id ile veya listeyle sorgulanır, **Then** yanıt
   onboarding'de üretilen aynı merchantKey'i içerir.
3. **Given** birden çok merchant oluşturulmuş, **When** key'leri karşılaştırılır, **Then** hiçbir iki
   merchant aynı merchantKey'e sahip değildir.
4. **Given** çağıran merchantKey'i kendisi göndermeye çalışır, **When** merchant oluşturulur, **Then**
   gönderilen değer yok sayılır ve sistem kendi ürettiği key'i döndürür.

---

### User Story 2 - Gateway merchantKey ile merchant'ı çözer (Priority: P2)

Gateway (veya ileride dış bir ödeme akışı) elindeki merchantKey ile merchant kaydını çözebilmeli —
key'i verip o key'e karşılık gelen merchant'ı (Id + temel bilgiler + durum) geri almalı.

**Why this priority**: Key'in var olması onu kullanışlı yapmaz; key'i merchant'a çevirebilmek amacının
kendisidir ve ödeme akışı geldiğinde ilk ihtiyaç budur. P1'den bağımsız test edilebilir, P1'e bağlıdır.

**Independent Test**: Bilinen bir merchantKey verildiğinde doğru merchant döner; var olmayan bir key
verildiğinde "bulunamadı" döner.

**Acceptance Scenarios**:

1. **Given** var olan bir merchantKey, **When** o key ile merchant sorgulanır, **Then** doğru merchant
   (Id, temel bilgiler, durum) döner.
2. **Given** var olmayan/boş/biçimsiz bir merchantKey, **When** sorgulanır, **Then** merchant
   döndürülmez ve anlaşılır "bulunamadı" sonucu döner (hata değil).
3. **Given** pasif/askıya alınmış bir merchant, **When** key ile sorgulanır, **Then** merchant döner ve
   mevcut durumu (status) görünür (durum değerlendirmesini çağıran yapar).

---

### Edge Cases

- **Key çakışması**: Üretilen key mevcut bir key ile çakışırsa sistem çakışmayı fark eder ve merchant
  benzersiz bir key alana kadar yeniden üretir; dışarıya çakışma sızmaz.
- **Değişmezlik**: Profil güncelleme (isim/e-posta/webhook vb.), durum değişikliği (aktif/pasif/askıya
  alma) veya başka hiçbir işlem merchantKey'i değiştiremez.
- **Silinmiş merchant**: Soft-delete edilmiş merchant, key ile aramada döndürülmez (mevcut registry
  davranışıyla tutarlı).
- **Tekrar okuma**: Key gizli olmadığından, tek-seferlik gösterim yoktur; istenildiğinde tekrar
  görüntülenebilir.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem her yeni merchant oluşturulduğunda otomatik olarak bir merchantKey MUST üretsin;
  key üretimi onboarding transaction'ının atomik parçasıdır ("oluşturuldu = key'i var").
- **FR-002**: merchantKey sunucu tarafında MUST üretilsin; çağıranın gönderdiği herhangi bir key değeri
  yok sayılır (merchant/istemci key seçemez).
- **FR-003**: Üretilen merchantKey tüm merchant'lar arasında benzersiz MUST olsun.
- **FR-004**: merchantKey merchant yaşam döngüsü boyunca değişmez (immutable) MUST olsun; hiçbir işlem
  onu güncelleyemez veya yeniden üretemez.
- **FR-005**: Sistem merchantKey'i kalıcı olarak MUST saklasın ve her zaman düz (okunabilir) olarak geri
  verebilsin — bu gizli bir credential değil, açık bir kimliktir.
- **FR-006**: Merchant oluşturma yanıtı üretilen merchantKey'i MUST içersin.
- **FR-007**: Merchant'ı Id ile veya listeyle döndüren sorgular merchantKey'i MUST içersin.
- **FR-008**: Sistem verilen bir merchantKey ile karşılık gelen merchant'ı çözmeyi (Id + temel bilgiler +
  durum) MUST sağlasın.
- **FR-009**: Var olmayan/boş/biçimsiz bir merchantKey ile arama, hata değil "bulunamadı" sonucu MUST
  döndürsün.
- **FR-010**: merchantKey URL ve log içinde güvenle taşınabilir MUST olsun (URL-güvenli, tek parça,
  boşluksuz).

### Key Entities

- **Merchant**: Gateway'in global merchant registry aggregate'i. Yeni değişmez bir kimlik alanı kazanır:
  **merchantKey** — sistemin onboarding'de mint ettiği benzersiz, stabil, açık dış kimlik. Mevcut `Id`
  (Guid) iç kimlik olmaya devam eder; merchantKey dış dünyaya dönük kimliktir.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Oluşturulan merchant'ların %100'ü boş olmayan bir merchantKey ile döner.
- **SC-002**: Merchant kümesinde key çakışması oranı %0'dır (benzersizlik her zaman korunur).
- **SC-003**: Geçerli bir merchantKey ile merchant çözme, key'i bilen bir çağıran için tek adımda (tek
  sorgu) tamamlanır.
- **SC-004**: Bir merchant'ın key'i, oluşturulduktan sonraki hiçbir güncelleme/durum işleminde değişmez
  (her zaman ilk üretilen değere eşit).

## Future Considerations *(kapsam dışı — sonraki dilimler)*

Bu dilim key'i üretir ve API yanıtlarında görünür kılar. Aşağıdakiler **bilinçli olarak ertelenmiştir**:

- **Merchant'a fiziksel teslim — self-servis portal**: Merchant kendi paneline girip key'ini görür.
  Merchant-facing kimlik doğrulama ister → **Identity BC** (henüz yok). Şimdilik key'i gateway operatörü
  Admin ekranından okuyup merchant firmasına band-dışı (sözleşme/e-posta) iletir.
- **Otomatik bildirim**: Gateway key'i merchant'ın kayıtlı `Email`/`WebhookUrl`'ine push eder. Bildirim
  altyapısı yok; ayrı dilim.
- **Payment akışına bağlama**: Payment.Api'nin ödeme çağrısında merchantKey alıp merchant'ı çözmesi/
  doğrulaması. Payment.Api şu an Merchant'ı hiç referanslamıyor; bu cross-BC iş Payment merchant
  farkındalığı kazandığı dilime ertelendi. Kullanıcının key'i oluşturma gerekçesi budur, ama bu dilimde
  uygulanmaz.
- **Admin UI'da gösterim**: Backend yanıtları key'i zaten içerir; admin ekranına eklenmesi ayrı UI dilimi.

## Assumptions

- **Anahtar tipi**: merchantKey açık bir *kimlik*tir, gizli bir *credential* değildir. 001'in Identity
  dilimine ertelediği gizli/hash'li API key'den ayrı ve ondan bağımsızdır. Düz saklanır, her zaman
  görüntülenebilir; tek-seferlik gösterim veya hash yoktur.
- **Kimlik otoritesi**: Key'i gateway (sistem) üretir, merchant değil. Merchant onboarding'de pasif
  taraftır; admin (gateway operatörü) kaydı açar, sistem key'i mint eder.
- **Format**: Key opak, URL-güvenli, tek parça bir dizedir. Gizli olmadığı için tahmin-edilemezlik bir
  güvenlik gereği değildir; benzersizlik ve stabilite yeterlidir. Kesin biçim/uzunluk plan aşamasında
  belirlenir.
- **Yetki**: Mevcut sistemle tutarlı olarak endpoint'ler korumasızdır (Authz Identity dilimine ertelendi).
- **Geriye dönük veri**: Bu dilimden önce oluşmuş merchant kaydı varsayılmaz (henüz seed/prod veri yok);
  var olan kayıtlara toplu key atama (backfill) kapsam dışıdır.