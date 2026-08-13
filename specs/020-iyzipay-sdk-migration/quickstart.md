# Quickstart: Iyzipay SDK Migration (020)

**Date**: 2026-08-13 | **Plan**: [plan.md](plan.md)

Taşımanın kanıt senaryoları. Ön koşul: .NET 10 SDK; iyzico kimlik bilgisi GEREKMEZ
(gerektiren her şey bilinçli kapsam dışı — FR-005).

## S1 — Çözüm derlenir (SC-001)

```bash
dotnet build
```

**Beklenen**: 0 hata. Derlenen projeler arasında `Iyzipay`, `Iyzipay.Samples`,
`Iyzipay.Tests`, `Iyzipay.Tests.Functional` görülür.

## S2 — Deterministik testler koşar (SC-002)

```bash
dotnet test tests/Iyzipay.Tests
```

**Beklenen**: Yeşil; hash/format testleri geçer, hiçbir test dış servise çıkmaz.

## S3 — Canlı testler ve Samples varsayılan koşuya GİRMEZ (SC-002)

```bash
dotnet test
```

**Beklenen**: Çözüm-geneli koşu yeşil. `Iyzipay.Tests.Functional` ve `Iyzipay.Samples`
test koşusunda LİSTELENMEZ (IsTestProject=false); mevcut BC testleri (Payment, Merchant,
Commission, Reference) önceki gibi geçer (SC-004).

## S4 — Kaynak izleniyor, çift kopya yok (SC-003, FR-006)

```bash
git check-ignore src/services/Iyzipay/Iyzipay.csproj; echo "ignore-durumu: $?"
ls src/otherProjects 2>&1
git status --porcelain | grep -c "src/services/Iyzipay"
```

**Beklenen**: `git check-ignore` eşleşme bulmaz (çıkış kodu 1 — dosya izleniyor);
`src/otherProjects` YOKTUR (No such file or directory); Iyzipay dosyaları git status'ta
eklenmiş görünür. `.gitignore`'da `src/otherProjects/` satırı kalmamıştır; `bin/`/`obj/`
izlenmez (FR-008).

## S5 — Mevcut projelere referans sızmadı (FR-007)

```bash
grep -rl "Iyzipay" src/services/Payment.Api src/services/Merchant.Api \
  src/services/Commission.Api src/services/Reference.Api src/aspire | wc -l
```

**Beklenen**: `0` — hiçbir BC/AppHost dosyası Iyzipay'e değinmez.

## Elle koşu (kapsam dışı, bilgi amaçlı)

Canlı sandbox testleri anahtar sağlanarak istenirse:

```bash
dotnet test tests/Iyzipay.Tests.Functional -p:IsTestProject=true
```

Anahtarsız koşu sandbox'tan hata döner — bu beklenen ve kapsam dışıdır.

## Notlar

- **Doğrulama sonucu (2026-08-13, implement)**: S1-S5 TÜMÜ GEÇTİ. S1: `dotnet build`
  0 hata. S2: Iyzipay.Tests 10/10 yeşil. S3: çözüm-geneli koşuda 5 test derlemesi
  (Functional ve Samples keşfedilmedi), toplam 226 test yeşil — taban çizgisiyle aynı
  (216 mevcut + 10 Iyzipay). S4: `check-ignore` çıkış 1, `src/otherProjects` yok,
  Iyzipay dosyaları izleniyor. S5: 0 referans.
- **Kaynak müdahalesi: SIFIR** (FR-004 tam korundu — research R6'daki asgari-düzeltme
  hakkı hiç kullanılmadı). Kalan uyarılar bilinçli: `RestHttpClientV2.cs` SYSLIB0014
  (`ServicePointManager` obsolete — davranış bozulmaz) ve `Sample.cs` çift-using
  (CS0105) uyarıları; ikisi de kaynak-koruma sözleşmesi gereği dokunulmadı.
- Sonraki adım (ayrı spec): Iyzico ödeme kanalı entegrasyonu (PosAccount-adayı,
  BankRouter katılımı).