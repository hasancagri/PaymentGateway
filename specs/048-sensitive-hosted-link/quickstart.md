# Quickstart / Doğrulama: Hassas Hosted Link

**Not**: Bu sürümde CANLI test bilinçli atlandı (kullanıcı kararı). Doğrulama = build + test. Aşağıdaki manuel senaryo ileride Aspire canlı doğrulaması için referans.

## Otomatik doğrulama (bu sürüm)

```bash
dotnet build          # 0 hata beklenir (Admin projesi söküldükten sonra)
dotnet test           # Merchant.Api.Tests SensitiveEntrySessionTests dahil geçmeli
```

Beklenen:
- `SensitiveEntrySessionTests` INV-1..INV-7 yeşil (Domain-TDD, test-first yazıldı).
- Çözüm Admin projesi referansı olmadan derlenir (slnx + AppHost temiz).
- Payment.Identity açılışında `admin-ui` prune log'u (test kapsamı dışı, canlıda görülür).

## Manuel senaryo (ileride, Aspire canlı)

Önkoşul: Aspire AppHost açık; bir Active merchant + `merchant.admin` scope'lu platform token.

1. **Link üret**: agent `admin_request_sensitive_link { merchantId }` çağırır → `{url, expiresAt, message}` döner. **Sohbet çıktısında hiçbir hassas alan yok** (SC-001).
2. **Görüntüle**: `url` tarayıcıda açılır → merchant'ın Email/GSM/TCKN/IBAN/vergi no değerleri sayfada görünür.
3. **Yenile**: sayfa F5 → link yaşar, değerler yine görünür (GET tüketmez).
4. **Düzenle**: bir alan (ör. IBAN) değiştir + kaydet → onay sayfası; değer kalıcı.
5. **Tek kullanım**: aynı link tekrar açılır → nötr 404 (SC-003).
6. **Süre**: yeni link üret, 15 dk bekle (veya TTL düşür) → nötr 404 (SC-002).
7. **Nötr red**: uydurma token + süresi geçmiş + tüketilmiş → üçü aynı 404 (SC-004).

## Referanslar

- Data model + invariant'lar: [data-model.md](./data-model.md)
- Tool + endpoint + HTML sözleşmesi: [contracts/sensitive-hosted-link.md](./contracts/sensitive-hosted-link.md)
- Kararlar/gerekçe: [research.md](./research.md)
- ECom emsali: ECommerce 078 credential-entry (PR #127 gömülü .html refactor)
