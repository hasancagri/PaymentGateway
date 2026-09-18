# Data Model: MCP-Only Admin Düzlemi (044)

Yeni aggregate/tablo/event YOK — DB şeması değişmez. Değişen şey SÖZLEŞMELER: alan
sınıflandırması ve hangi kanalın hangi alanı taşıyabildiği.

## Alan sınıflandırması (Merchant)

| Sınıf | Alanlar | MCP girdi/çıktı | Hassas-veri sayfası (BFF) |
|---|---|---|---|
| Hassas (kişisel/finansal) | Email, GsmNumber, IdentityNumber (TCKN), Iban, TaxNumber | ASLA | Görüntüle + düzenle |
| Sır | MerchantKey, SubMerchantKey | ASLA | YOK (mevcut düzen: MerchantScoped GetMerchant döner, 023 dev kararı) |
| Hassas-dışı | Name, Type, Address, ContactName, ContactSurname, TaxOffice, LegalCompanyTitle | Serbest | Taşınmaz (yalnız ad başlıkta gösterilebilir) |
| Sistem | MerchantId, Status, CreatedTime | Serbest (salt-okuma) | Salt-okuma |

- ContactName/Surname hassas-dışı: kullanıcı çizgisi (oturum kararı, spec Assumptions).
- Merchant-düzlemi `GetMerchant` (MerchantScoped) bu sınıflandırmanın DIŞINDA — merchant kendi
  verisini okur, sözleşmesi değişmez.

## Aggregate davranış eşlemesi

| İşlem | Kanal | Aggregate metodu | Not |
|---|---|---|---|
| Hassas-dışı güncelleme | MCP `admin_update_merchant` | `Merchant.UpdateDetails` | Handler hassas alanları mevcut değerden geçirir (research R3) |
| Hassas güncelleme | BFF `UpdateMerchantSensitive` | `Merchant.UpdateDetails` | Handler hassas-dışı alanları mevcut değerden geçirir |
| Politika oluşturma | MCP `admin_create_commission_policy` | `CommissionPolicy.Create` | Tekil-aktif kuralı handler sorgusuyla (024 deseni aynen) |
| Marj güncelleme | MCP `admin_update_commission_margin` | `CommissionPolicy.UpdateMargin` | |
| Politika statüsü | MCP `admin_change_commission_status` | `CommissionPolicy.ChangeStatus` | Statü geçiş kuralları aggregate'te |

Aggregate'lere YENİ metot eklenmez; mevcut davranışlar çağrılır (043 kuralıyla aynı).

## Sözleşme değişimleri (mevcut MCP tool'ları)

| Tool | Değişiklik |
|---|---|
| `admin_get_merchants` | `MerchantItem`'dan Email + GsmNumber ÇIKAR |
| `admin_get_pending_registrations` | Yanıt öğesinden Email + GsmNumber ÇIKAR (başvuru sahibi kişisel verisi) |
| Diğer tüm mevcut tool'lar | DEĞİŞMEZ |

## Silinen sözleşmeler

- REST: `POST/PUT /merchants`, `PUT /merchants/{id}/status`, `GET /merchants` (liste);
  `GET/POST /register-requests*` (liste/approve/reject); `POST /commission-policies`,
  `PUT .../margin`, `PUT .../status`, `GET /commission-policies` (liste), `GET .../calculate`.
- OAuth seed: `merchant-agent`, `payment-agent` client kayıtları (Identity.Server Config).
- KALAN REST: `GET /merchants/{id}` + `GET /commission-policies/{merchantId}` (MerchantScoped),
  `POST /merchants/activation/redeem` (S2S), yeni sensitive çifti (aşağıda, contracts/).

## Durum makineleri

Değişiklik YOK — Merchant statü makinesi ve CommissionPolicy statü geçişleri aynen; yalnız
tetikleme kanalı değişiyor (REST → MCP).