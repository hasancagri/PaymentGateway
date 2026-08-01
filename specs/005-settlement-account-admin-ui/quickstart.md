# Quickstart / Validation: Settlement Hesabı Ekranları (005)

Ekranların uçtan uca çalıştığını doğrulama rehberi. Detaylar: [contracts/screens.md](./contracts/screens.md),
[data-model.md](./data-model.md). Kullanıcı: gateway admin.

## Önkoşullar

- Sistem Aspire ile ayakta: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
  (Postgres + RabbitMQ + merchant-api + commission-api + admin-web).
- Aspire dashboard'dan **admin-web** URL'ini aç.
- En az bir mevcut `Merchant` (yoksa Admin → Merchants → "Yeni" ile oluştur, bkz. 001).
- Geçerli TR IBAN (mod-97 doğru), örn. `TR460010000000000000000001`.

## Senaryo 1 — Hesapları gör + boş durum (US1 / SC-001)

1. Admin → bir merchant'ın **Detay** sayfası → **"Settlement Hesapları"** butonu.
2. Hiç hesap yoksa: "hesap yok + yeni ekle" bilgisi görünür (boş = hata değil).
**Beklenen**: sayfa açılır, merchant seçili gelir, boş-durum gösterilir.

## Senaryo 2 — Hesap ekle (US2 / SC-002)

1. "Yeni hesap" → Create formu.
2. Banka dropdown'dan seç (örn. `0010 — Ziraat Bankası`), geçerli TR IBAN, sahip adı gir, kaydet.
**Beklenen**: "Hesap eklendi" + Index'te yeni satır (banka kod+ad, IBAN, sahip, **Active**).

## Senaryo 3 — Doğrulama hataları (US2 / SC-003)

- Bozuk IBAN (`TR00`) ile kaydet → IBAN format hatası (Türkçe), hesap eklenmez, form girdileri durur.
- Aynı IBAN'ı ikinci kez ekle → mükerrer kayıt hatası.
- (Banka dropdown'dan seçim zorunlu → katalog dışı kod UI'de girilemez; FR-006.)
**Beklenen**: her hata `_Messages`'ta anlaşılır Türkçe; kısmi/hatalı kayıt oluşmaz.

## Senaryo 4 — Tenant izolasyonu (US1 / SC-004)

1. A merchant'ına hesap ekle; B merchant'ını seç.
2. B'nin Index'i A'nın hesabını GÖSTERMEZ.
3. URL'e elle A'nın accountId'sini B rotasında yaz (`.../Edit?merchantId=B&accountId=A-account`).
**Beklenen**: B listesinde A yok; cross-tenant Edit → "bulunamadı" (404, sızıntı yok).

## Senaryo 5 — Güncelle (US3)

1. Bir hesabın **Düzenle**'sine gir, geçerli yeni IBAN + sahip gir, kaydet.
2. Index yeni değerleri gösterir.
3. Bozuk IBAN'la güncelle → hata; eski değerler korunur (Index eski değeri gösterir).
**Beklenen**: geçerli güncelleme yansır; geçersiz reddedilir, eski değer bozulmaz.

## Senaryo 6 — Aktif/Pasif (US3 / SC-005)

1. Edit'te **"Pasife al"** → durum `Passive`.
2. Index'te satır **Passive** rozetiyle DURUR (silinmez).
3. **"Aktif et"** → `Active`.
**Beklenen**: durum değişir, kayıt hiçbir aşamada silinmez.

## Doğrulama tamam sayılır

- [ ] 6 senaryo beklenen sonuçları verir.
- [ ] Banka yalnız dropdown'dan seçilir (serbest kod yok).
- [ ] Tenant izolasyonu: cross-merchant liste boş / Edit 404.
- [ ] Hatalar Türkçe, form girdileri korunur, kısmi kayıt yok.
- [ ] Backend değişmedi (yalnız `src/ui/Admin` diff'i).
- [ ] `dotnet build` yeşil; Aspire ayağa kalkar.