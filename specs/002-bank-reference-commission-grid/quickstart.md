# Quickstart: Bank Referansı + Komisyon Grid

## Ön koşul

Sistem Aspire ile ayağa: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
(Postgres + RabbitMQ + Commission.Api + Admin). Bankalar boş başlar (seed yok).

## Doğrulama akışı

1. **Banka ekle**: Admin → "Bankalar" → Yeni. Kod `0062`, ad `Garanti BBVA`, taksitler
   `1,2,3,6,9,12`. Kaydet → listede görünür.
2. **Kopya reddi**: Aynı kod `0062` ile tekrar ekle → "zaten var" hatası.
3. **Güncelle**: `0062` düzenle, taksitleri `1,2,3,6` yap, aktif bırak → kaydet. Kod alanı değişmez.
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
- `Create` geçerli/geçersiz (kod uzunluğu, boş ad, boş/aralık-dışı taksit)
- `Update` kod değişmezliği + doğrulama
- `SoftDelete` bayrak + zaman

Detay kontrat: [contracts/banks-api.md](./contracts/banks-api.md),
[contracts/bank-commissions-bulk-api.md](./contracts/bank-commissions-bulk-api.md).