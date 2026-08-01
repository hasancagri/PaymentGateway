# Research: Settlement Hesabı Yönetim Ekranları (005)

Phase 0 — teknik bağlam netleştirme. Salt-UI feature; kararlar mevcut Admin BFF desenleri +
004 API + anayasa ile hizalı. NEEDS CLARIFICATION kalmadı.

Aktör notu: ekranların kullanıcısı **gateway admin** (platform-tarafı yönetici), merchant değil.
Bu Admin BFF iç bir yönetim panelidir (merchant self-service değil); tenant seçimi admin'de kalır.

## D1 — Banka dropdown listesinin kaynağı (backend'e dokunmadan)

**Decision**: Ekleme/düzenleme formundaki banka seçimi, mevcut `Commission.Api` `GET
/api/v1/banks/catalog?onlyAvailable=false` endpoint'inden **canlı okunur** (`ICommissionApiClient.
GetBankCatalogAsync(onlyAvailable: false)`). Bu, kanonik banka kod+ad listesini döndürür — Merchant BC'nin
yerel `BankCatalog`'unun senkron tutulduğu aynı liste.

**Rationale**:
- FR-011: bu feature Merchant.Api'yi DEĞİŞTİREMEZ. Merchant BC settlement için bir katalog endpoint'i
  açmıyor; eklemek backend değişikliği olurdu.
- Admin BFF zaten bir kompozisyon katmanı ve iki API'yi (Merchant + Commission) çağırıyor. Katalogu
  Commission'dan okumak yeni bir mimari kural getirmez; anayasa I (BC izolasyonu) BFF'nin çok API
  çağırmasına engel değil (DB/aggregate erişimi yasak, HTTP kompozisyon serbest).
- `onlyAvailable=false` tüm katalog girişlerini verir (true, yalnız Commission'da Bank olarak
  eklenmemişleri filtreler — settlement için istemediğimiz filtre).

**Alternatives considered**:
- **Merchant.Api'ye `GET .../settlement-accounts/banks` ekle**: FR-011 ihlali (backend değişir). Red.
- **Admin UI'de statik banka listesi**: üçüncü kopya + elle senkron; Commission endpoint'i zaten
  canlı ve hazır. Red.

**Bedeli (kabul edildi)**: Commission katalogu ile Merchant'ın yerel kopyası teorik olarak drift
edebilir → dropdown'da görünen bir banka Merchant.Api doğrulamasında reddedilebilir. İkisi de aynı
kaynaktan (CP.VPOS) türer; drift nadir ve API hata mesajı (`RECORD_NOT_FOUND`) zaten kullanıcıya
gösterilir. Tolere edilebilir.

## D2 — Ekran deseni ve sayfa yapısı

**Decision**: Üç Razor Pages sayfası, `Pages/SettlementAccounts/` altında:
- **Index**: merchant dropdown (MerchantCommissions/Index deseni) + seçilince hesap tablosu.
- **Create**: banka dropdown (Banks/Create deseni) + IBAN/sahip/hesapNo/açıklama form; POST → API.
- **Edit**: dolu form + ayrı aktif/pasif aksiyonu; PUT + PUT status.

Giriş noktası: `Merchants/Details.cshtml` aksiyon barına buton (mevcut "Komisyonları" bağlantısının
yanına), `asp-route-merchantId` ile. Gateway admin merchant detayından o merchant'ın hesaplarına geçer.

**Rationale**: Mevcut iki deseni (merchant-scoped liste + katalog-dropdown'lı form) birleştirir;
admin için tanıdık düzen (FR-012). Detay sayfasından giriş, MerchantCommissions'ın merchant-seç
akışıyla tutarlı.

**Alternatives considered**:
- **Tek sayfada liste+form (inline)**: MerchantCommissions grid'i gibi tek-sayfa; ama settlement CRUD
  ayrık kayıtlar (grid değil), ayrı Create/Edit sayfaları daha net. Red.
- **Modal/JS ağırlıklı**: Admin paneli sunucu-render + minimal JS; mevcut desenle tutarsız. Red.

## D3 — Typed client ve DTO'lar

**Decision**: Yeni `ISettlementAccountApiClient` + `SettlementAccountApiClient : ApiClientBase`,
`BaseAddress = http://merchant-api` (Aspire service discovery, `Program.cs`'te `AddHttpClient`).
Merchant-scoped rotalar: `POST/GET /api/v1/merchants/{merchantId}/settlement-accounts`, `GET/PUT
/{accountId}`, `PUT /{accountId}/status`. DTO'lar `ApiModels.cs`'e eklenir (mevcut record/class stili).

**Rationale**: `MerchantApiClient`/`CommissionApiClient` deseniyle birebir. `ApiClientBase.SendAsync`
zaten zarf okuma + transport-hatası → `SERVER_ERROR` sağlar; yeniden kullanılır.

**Alternatives considered**:
- **Var olan `MerchantApiClient`'a ekle**: settlement ayrı bir kaynak; ayrı client ilgiyi ayırır ve
  `MerchantApiClient` şişmez. Ayrı client tercih (mevcut Commission/Merchant ayrımıyla tutarlı).

## D4 — Durum (aktif/pasif) UI eylemi

**Decision**: Edit sayfasında hesabı aktif/pasif yapan ayrı bir buton/aksiyon (`PUT
/{accountId}/status`, gövde `{ isActive }`). Liste satırında durum rozet olarak gösterilir; toggle
Edit üzerinden.

**Rationale**: 004 status endpoint'i ayrı (`SetSettlementAccountStatus`); UI onu ayrı aksiyonla
çağırır. Silme YOK (soft; FR-009/SC-005). Liste sade kalır.

**Alternatives considered**:
- **Liste satırında satır-içi toggle**: mümkün ama her satıra form + POST; Edit'te toplamak daha basit
  ve mevcut Banks Edit deseniyle tutarlı. Red (şimdilik).

## D5 — Doğrulama ve hata gösterimi

**Decision**: UI ek doğrulama koymaz (FR-007/assumption); form API'ye gönderilir, dönen `ApiMessage`
kodları `MessageText` ile Türkçe'ye çevrilip `_Messages` partial'ında gösterilir. Hata durumunda form
girdileri korunur (`BindProperty` + `return Page()`), MerchantCommissions/Banks Create deseni.

**Rationale**: Tek doğrulama otoritesi 004 aggregate/handler (anayasa II). UI kopya kural tutmaz;
INVALID_FORMAT/RECORD_NOT_FOUND/RECORD_DUPLICATE kodları `MessageText` sözlüğünde olmalı (yoksa
implement sırasında eklenir).

**Alternatives considered**:
- **Client-side IBAN regex**: çift bakım + drift; mod-97'yi UI'de tekrar etmek anlamsız. Red.

## D6 — Aktör: gateway admin (merchant değil)

**Decision**: Ekranların tek kullanıcısı gateway admin. Merchant kendi hesaplarını bu panelden
yönetmez (self-service değil). Admin herhangi bir merchant'ı seçip onun adına hesap yönetir; tenant
sınırı yine rota `{merchantId}` ile korunur.

**Rationale**: Kullanıcı direktifi. Payout hesabı platformun para akışı için kritik; gateway
operasyonunun sorumluluğu. Merchant-facing self-service ileride ayrı bir arayüz/BC olur (bu kapsamda değil).

**Etki**: Yetki modeli netleşince (TODO AUTHZ_MODEL) bu panel gateway-admin yetkisiyle korunur; merchant
scope'lu token'a açılmaz. Şimdilik yetki yok (001–004 ile tutarlı).

## Çözülen bilinmeyenler

| Konu | Sonuç |
|------|-------|
| Aktör | Gateway admin (iç panel; merchant self-service değil) |
| Banka dropdown kaynağı | Commission.Api `/banks/catalog?onlyAvailable=false` (canlı) |
| Backend değişikliği | YOK (FR-011) — yalnız `src/ui/Admin` |
| Yeni paket | YOK (CPM korunur) |
| Durum eylemi | Edit'te ayrı aktif/pasif aksiyonu (PUT status); silme yok |
| UI doğrulama | Yok; API sonucu gösterilir (`MessageText` Türkçe) |
| Test | Otomatik UI testi yok; quickstart elle doğrulama |
| Yetki | Ertelendi; ileride gateway-admin rolü; tenant sınırı rota merchantId ile |