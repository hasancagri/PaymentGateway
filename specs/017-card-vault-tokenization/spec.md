# Feature Specification: Card Vault / Tokenization (Kart Saklama)

**Feature Branch**: `017-card-vault-tokenization`

**Created**: 2026-08-10

**Status**: Draft

**Input**: User description: "Kart saklama / tokenizasyon (Payment BC — Card Vault). PCI kapsam daraltma; ECommerce ham 16-hane PAN'ı ASLA persist etmez, Gateway kartın kayıt-otoritesidir. Server-to-server tokenize; Gateway yalnız token döner ve yalnız merchantId bilir."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Kart tokenize et ve ödemede kullan (round-trip) (Priority: P1)

Bir merchant'ın son-kullanıcısı ECommerce sitesinde "kart ekle" der. ECommerce backend PAN'ı
anlık alır, `last4`/`brand`'i kendi çıkarır, PAN'ı Gateway'e gönderir ve dönen opak `token`'ı
kendi tarafında `(UserId, token, last4, brand)` olarak saklayıp PAN'ı atar. Sonraki ödemede
ECommerce token'ı payment session'a verir; Gateway token'ı gerçek kart bilgisine çözer ve
mevcut banka yönlendirme akışını besler.

**Why this priority**: Bu round-trip olmadan feature değersiz. PAN'ın ECommerce'te kalıcı
tutulmamasını sağlayan asıl güvenlik kazanımı ve ödeme akışının token'la beslenmesi burada.
Tek başına teslim edilse bile MVP: merchant kart saklayıp ödeme geçebilir.

**Independent Test**: Bir merchant token'ıyla PAN gönderip token al; aynı token'ı çözüm/ödeme
yolunda kullanıp doğru BIN/kart bilgisinin döndüğünü doğrula. PAN'ın Gateway dışına (yanıtta,
log'da, ECommerce persist'inde) hiç çıkmadığını gözle.

**Acceptance Scenarios**:

1. **Given** Active bir merchant, **When** geçerli PAN + son kullanma + kart sahibi ile tokenize
   isteği gönderir, **Then** yalnız opak bir `token` döner (PAN/last4/brand yanıtta YOK), kart
   Gateway'de saklanır (`Active` statü).
2. **Given** saklanmış bir token, **When** merchant o token'ı ödeme/çözüm akışında kullanır,
   **Then** Gateway token'ı gerçek karta çözer, BIN/kart programı yönlendirmeye aktarılır.
3. **Given** geçersiz (Luhn'dan geçmeyen) PAN, **When** tokenize denenir, **Then** iş-kuralı
   hatası döner, kart saklanmaz.
4. **Given** son kullanma tarihi geçmiş kart, **When** tokenize denenir, **Then** doğrulama
   hatası döner.

---

### User Story 2 - Kartı sil (soft revoke) (Priority: P2)

Son-kullanıcı ECommerce'te kartını siler; ECommerce kendi kaydını siler ve Gateway'e iptal
isteği yollar. Gateway token'ı `Revoked` işaretler (fiziksel silmez — Fraud/audit geçmişi
korunur). İptal edilmiş token artık ödeme/çözüm akışında kullanılamaz.

**Why this priority**: Kullanıcı kart yönetimi için gerekli; silme olmadan saklama eksik. Ama
tokenize+kullanım round-trip'i çalışmadan anlamlı değil, o yüzden P2.

**Independent Test**: Bir token'ı revoke et; sonra o token'la çözüm/ödeme dene → reddedildiğini
doğrula. Kayıt fiziksel durur (Revoked statü) doğrula.

**Acceptance Scenarios**:

1. **Given** Active bir token, **When** sahibi merchant revoke eder, **Then** statü `Revoked`
   olur, kayıt fiziksel silinmez.
2. **Given** `Revoked` bir token, **When** ödeme/çözüm akışında kullanılır, **Then** reddedilir.
3. **Given** zaten `Revoked` bir token, **When** tekrar revoke edilir, **Then** idempotent
   başarı (hata değil).

---

### User Story 3 - Kartı güncelle (expiry + kart sahibi) (Priority: P3)

Son-kullanıcı kartının son kullanma tarihini/sahip adını günceller (kart yenilendi, aynı PAN).
ECommerce Gateway'e güncelleme yollar; Gateway aynı token üzerinde `expiry`/`holderName`'i
değiştirir. PAN DEĞİŞTİRİLEMEZ — PAN değişimi yeni kart demektir (sil + yeni tokenize).

**Why this priority**: Kolaylık; kart yenilenince yeni token üretmek yerine güncelleme. Kritik
değil, P3.

**Acceptance Scenarios**:

1. **Given** Active bir token, **When** yeni geçerli `expiry` + `holderName` ile güncellenir,
   **Then** token aynı kalır, alanlar güncellenir.
2. **Given** güncelleme isteğinde PAN alanı, **When** gönderilir, **Then** kabul edilmez
   (güncelleme PAN taşımaz; PAN değişimi sil+ekle akışıdır).
3. **Given** `Revoked` bir token, **When** güncellenmeye çalışılır, **Then** reddedilir.

---

### Edge Cases

- **Bilinmeyen token** çözüm/ödeme akışında → bulunamadı, akış güvenli başarısız.
- **Cross-merchant token**: merchant A'nın token'ını merchant B kullanmaya/silmeye/güncellemeye
  çalışır → fail-closed reddedilir (token sahibi merchant ile istek merchant'ı eşleşmeli).
- **Provisioning merchant** vault ucunu çağırır → ödeme-düzlemi kapalı (Active değil) → reddedilir.
- **Aynı PAN ikinci kez tokenize** → yeni ve farklı token üretilir (idempotent DEĞİL); dedup
  ECommerce'in UserId tarafında.
- **Log/hata mesajı** → hiçbir yerde tam PAN görünmez; en fazla `last4`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, verilen PAN + son kullanma + kart sahibi adından opak bir `token` üretip
  kartı Gateway (Payment BC) tarafında saklamak ZORUNDADIR.
- **FR-002**: Tokenize yanıtı YALNIZCA `token` içermek ZORUNDADIR; PAN, `last4`, `brand`, `bin`
  yanıtta DÖNMEZ.
- **FR-003**: Ham PAN Payment BC dışına (HTTP yanıtı, log, event, başka BC) HİÇBİR biçimde
  çıkarılMAZ; PAN slice/domain sınırını ham geçmez (CP.VPOS kuralıyla tutarlı).
- **FR-004**: Saklanan her kart en az şu alanları taşımak ZORUNDADIR: `token`, `merchantId`,
  korunan PAN (dev'de enc-at-rest simüle), `expiry`, `bin`, `last4`, `brand`, `status`.
- **FR-005**: `token` opak ve tahmin-edilemez OLMAK ZORUNDADIR; PAN'dan türetilMEZ.
- **FR-006**: PAN, tokenize sırasında Luhn kontrolünden geçmek ZORUNDADIR; geçmezse kart
  saklanmaz.
- **FR-007**: Son kullanma tarihi geçmiş kart tokenize/güncelleme sırasında reddedilmek
  ZORUNDADIR.
- **FR-008**: Her saklanmış kart bir `merchantId`'ye bağlı OLMAK ZORUNDADIR; Gateway
  son-kullanıcı kimliğini (UserId) BİLMEZ ve tutmaz.
- **FR-009**: Tüm vault uçları (tokenize/çözüm/güncelle/sil) çağıran merchant ile kartın
  `merchantId`'sinin eşleşmesini zorunlu kılmak ZORUNDADIR; eşleşmezse fail-closed reddedilir
  (cross-merchant erişim yok).
- **FR-010**: Sistem, saklanmış `token`'ı gerçek kart bilgisine (BIN/kart programı) çözerek
  mevcut ödeme yönlendirme akışını beslemek ZORUNDADIR (mevcut çözüm sözleşmesi değişmez).
- **FR-011**: Kart silme SOFT olmak ZORUNDADIR: kayıt `Revoked` işaretlenir, fiziksel silinmez
  (Fraud/audit geçmişi korunur).
- **FR-012**: `Revoked` bir token ödeme/çözüm akışında kullanılamaz.
- **FR-013**: Kart güncelleme YALNIZ `expiry` + `holderName` değiştirmek ZORUNDADIR; PAN
  güncelleme ile değiştirilEMEZ.
- **FR-014**: Aynı merchant aynı PAN'ı ikinci kez tokenize ederse sistem yeni ve farklı bir
  `token` üretmek ZORUNDADIR (tokenizasyon idempotent DEĞİL).
- **FR-015**: Revoke işlemi idempotent OLMAK ZORUNDADIR (zaten Revoked kart tekrar revoke →
  başarı, hata değil).
- **FR-016**: Vault durum-değiştiren uçları özel bir vault yetkisi (`cards.write` capability
  scope) + tenant eşleşmesi (MerchantScoped) altında OLMAK ZORUNDADIR; beklenen hatalar Result
  deseniyle taşınır. Bu yetki ödeme MCP/POS uçlarını (`payment.write`) merchant'a AÇMAZ.
- **FR-017**: Vault uçları yalnız ödeme-yapabilir (Active) merchant'a açık OLMAK ZORUNDADIR;
  Provisioning statüsünde çağrı reddedilir.

### Key Entities *(include if feature involves data)*

- **StoredCard (Vault kaydı)**: Gateway'in kartın kayıt-otoritesi olarak tuttuğu kayıt. Kimliği
  opak `token`. Sahibi `merchantId`. Korunan PAN (dev enc-at-rest simüle) + `expiry` + `bin` +
  `last4` + `brand` + `status` (`Active` / `Revoked`). Davranışlar: tokenize (create),
  güncelle (expiry/holder), revoke (soft). PAN ve token immutable; PAN değişimi yeni kayıt.
- **Merchant (referans)**: Yalnız `merchantId` olarak var; kart sahipliğinin sınırı. Payment
  BC'de zengin model değil, kimlik referansı.
- **Payment token akışı (mevcut)**: Ödeme/çözüm token'ı kart bilgisine çevirir; bu feature
  simüle fixture yerine gerçek saklanmış kartları besler.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Kart ekleme sonrası ECommerce tarafında ham PAN'ın hiçbir kalıcı kayıtta
  bulunmadığı %100 doğrulanabilir (yalnız token + last4 + brand saklanır).
- **SC-002**: Tokenize→ödeme round-trip'i uçtan uca çalışır: saklanmış token %100 doğru
  kart/BIN'e çözülür.
- **SC-003**: Bir merchant'ın token'ı başka merchant tarafından **yazım uçlarında**
  (tokenize/sil/güncelle) kullanılamaz — cross-merchant deneme %100 reddedilir. (Ödeme-anı/resolve
  cross-merchant eşleşmesi charge feature'ına ertelendi — bkz. research R3; PaymentSession
  merchantId taşımıyor.)
- **SC-004**: Silinen (Revoked) kart ödeme akışında %100 reddedilir; kayıt fiziksel korunur.
- **SC-005**: Tam PAN hiçbir HTTP yanıtında, log satırında veya integration event'te görünmez
  (en fazla last4).

## Assumptions

- **Şifreleme dev'de simüle**: Gerçek HSM/KMS kapsam dışı; PAN "enc-at-rest" davranışı dev'de
  simüle edilir (kalıcılık ve sınır kuralları gerçek, kripto sonraya).
- **last4/brand ECommerce'te türetilir**: ECommerce PAN'ı anlık gördüğü için `last4`/`brand`'i
  kendi çıkarır; Gateway yanıtı yalnız token döndürür (kullanıcı kuralı).
- **Vault Active-only**: Tokenize dahil vault uçları ödeme-düzlemidir; yalnız Active merchant
  erişir. Provisioning demeti (kendi kaydı + settlement + ReturnUrl) vault'u içermez. Bu, "charge
  fail-closed" ilkesiyle tutarlı ödeme-düzlemi kapısıdır (anayasa V, 013). Tokenize ≠ charge;
  charge bu feature'ın dışında (007'de ertelenmiş).
- **007 çözüm sözleşmesi değişmez**: Token→kart çözümü mevcut arayüz üzerinden gider; yalnız
  arkadaki kaynak simüle fixture'dan gerçek vault kaydına döner.
- **Yalnız TL**: Para birimi modellenmez (anayasa alan kısıtı); kart saklamada para birimi yok.
- **Son-kullanıcı yönetimi ECommerce'te**: Kart listeleme, default kart seçimi, UserId eşlemesi
  ECommerce sorumluluğu; Gateway yalnız token-bazlı kayıt tutar.

## Out of Scope

- Gerçek HSM/KMS tokenizasyonu ve prod-grade kripto (dev simüle).
- Hosted fields / iframe (Seçenek B — PAN'ın ECommerce'e hiç uğramaması); ertelendi.
- Ödeme çekimi (charge/ProcessPayment) — 007'de ertelendi.
- Fraud tespiti — ödeme altyapısı oturduktan sonra ayrı döngü.
- ECommerce tarafındaki kart yönetimi ekranları/tablosu (ECommerce reposu işi).
- MerchantKey rotate feature'ı (ayrı karar/spec).