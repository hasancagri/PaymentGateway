# Quickstart: Iyzico Payment Channel — Yapısal Eritme (022)

**Date**: 2026-08-13 | **Plan**: [plan.md](plan.md)

Canlı/sandbox doğrulama YOK (kullanıcı kararı). Tüm senaryolar derleme + tarama.

## S1 — Çözüm derlenir (SC-001)

```bash
dotnet build
```

**Beklenen**: 0 hata. Çözümde CP.VPOS ve Iyzipay* projeleri YOK; Payment/Merchant/
Commission yeni Domains/Provider yapısıyla derlenir.

## S2 — Kalıntı taraması (SC-002)

```bash
grep -rniE "CP\.VPOS|CPVPOS|BankRouter|PosAccount|BinCard|Iyzipay" \
  src tests PaymentGateway.slnx .gitignore --include="*.cs" --include="*.csproj" \
  --include="*.cshtml" --include="*.slnx" 2>/dev/null | grep -v "specs/" | grep -v "/obj/\|/bin/"
```

**Beklenen**: 0 satır (Admin'in kendi DTO/sayfa adları dahil — ölü ekran kalıntısı da
taranır; bulunanlar temizlenir veya bilinçli istisna olarak burada not edilir).

## S3 — Testler (SC-003)

```bash
dotnet test
```

**Beklenen**: Koşacak test projesi kalmadı (5'i silindi) — komut hatasız biter, 0 test.
(023/024 yeni domain testleri getirecek.)

## S4 — Sınır sızması yok (SC-004)

```bash
grep -rln "Provider" src/ui/Admin src/agents src/others --include="*.cs" | grep -v obj
```

**Beklenen**: Sağlayıcı tiplerine (BC `Provider`/Domains sağlayıcı modelleri) Payment/
Merchant/Commission DIŞINDA referans yok.

## Notlar

- **Doğrulama sonucu (2026-08-13, implement)**: S1 GEÇTİ (build 0 hata; CP.VPOS + 4 Iyzipay*
  + SharedKernel + Excel.Mcp + 3 BC test projesi çözümden çıktı — proje sayısı 12'ye indi).
  S2 GEÇTİ (kalıntı 0; bilinçli istisna: iyzico protokol değerleri `iyzipay-dotnet-2.1.78` ve
  `*.iyzipay.com` URL'leri). S3 GEÇTİ (koşacak test projesi yok, komut hatasız). S4 GEÇTİ
  (sağlayıcı tiplerine BC dışından 0 referans).
- Kullanıcı ek silmeleri (CardVault, SharedKernel, Excel.Mcp) tasks.md Uygulama notunda.
- Bilinçli ara durum: üç BC uçsuz (endpoint'siz) derlenir; Admin ekranları ve agent'lar
  derlenir-ama-ölü. Çalışır akış sonraki spec'lerin işi (023 SubMerchant, 024 komisyon,
  ödeme akışı ayrıca).