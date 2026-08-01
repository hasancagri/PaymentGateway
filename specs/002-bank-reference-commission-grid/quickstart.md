# Quickstart: Bank Referansı + Komisyon Grid

## Ön koşul

Sistem Aspire ile ayağa: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
(Postgres + RabbitMQ + Commission.Api + Admin). Bankalar boş başlar (seed yok).

## Doğrulama akışı

1. **Banka ekle**: Admin → "Bankalar" → Yeni. Katalog selectbox'ından `0062 — Garanti BBVA` seç
   (kod/ad elle girilmez), taksitleri 1..15 checkbox grid'inden `1,2,3,6,9,12` işaretle. Kaydet →
   listede görünür (ad `Garanti BBVA` katalogdan gelir).
2. **Kopya reddi**: Selectbox `0062`'yi artık listelememeli (eklenmiş); doğrudan tekrar ekleme
   denemesi → "zaten var" hatası.
3. **Güncelle**: `0062` düzenle — kod ve ad salt-görünüm (değiştirilemez); taksit checkbox'larından
   `1,2,3,6` bırak, aktif kalsın → kaydet.
4. **Grid**: "Banka Komisyonları" → Yeni. Banka dropdown'dan `0062` seç. Grid
   VISA/MC/TROY/AMEX × CREDIT/DEBIT/PREPAID × DOMESTIC/INTERNATIONAL × {1,2,3,6} satırlarını gösterir.
   Tüm hücreler başta **eksik** işaretli.
5. **Toplu kaydet**: Birkaç hücreye oran gir (ör. VISA/CREDIT/DOMESTIC/3 → 1.75), kaydet.
   Grid'e dönünce o hücreler dolu, kalanlar eksik.
6. **Eksik kapanışı**: Tüm hücreleri doldur → kaydet → grid'de hiç "eksik" kalmaz (SC-004).
7. **Silme guard**: `0062`'yi silmeye çalış → komisyon bağlı olduğundan reddedilir
   ("önce komisyonları sil"). Komisyonsuz başka bankayı sil → başarı.

## Test doğrulaması

`dotnet test tests/Commission.Api.Tests` — `BankTests` yeşil:
- `Create` geçerli/geçersiz (kod uzunluğu, katalog-dışı kod, boş/aralık-dışı taksit; Name katalogdan)
- `Update` kod+ad değişmezliği + taksit doğrulama
- `SoftDelete` bayrak + zaman
- `BankCatalog` arama (var/yok kod)

Detay kontrat: [contracts/banks-api.md](./contracts/banks-api.md),
[contracts/bank-commissions-bulk-api.md](./contracts/bank-commissions-bulk-api.md).