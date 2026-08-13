# Quickstart: SubMerchants Yapısal DDD Geçişi (025)

Yapısal geçişi kanıtlayan doğrulama. Davranış yok → canlı senaryo yok; doğrulama **grep + build +
test**. Detay: [data-model.md](./data-model.md), [plan.md](./plan.md).

## Ön koşullar

- Repo kökünde, `dotnet` 10 kurulu. Aspire/DB gerekmez (yapısal + saf domain test).

## Doğrulama S1 — Sağlayıcı tipleri `Domains/`'den çıktı (SC-001)

```bash
grep -rlE "class .*: *(BaseRequestV2|ProviderResourceV2)" src/services/Merchant.Api/Domains
```
Beklenen: **çıktı yok** (boş) — `Domains/` altında sağlayıcı-türeyen tip kalmadı.

```bash
test -d src/services/Merchant.Api/Domains/SubMerchants && echo "HALA VAR (HATA)" || echo "klasor dagitildi (OK)"
```
Beklenen: `klasor dagitildi (OK)`.

```bash
ls src/services/Merchant.Api/Provider/Onboarding/
```
Beklenen: `SubMerchant.cs  CreateSubMerchantRequest.cs  UpdateSubMerchantRequest.cs  RetrieveSubMerchantRequest.cs  SubMerchantType.cs`.

## Doğrulama S2 — Aggregate-klasör kuralı korunur (SC-002)

```bash
grep -rlE "class .*: AggregateRoot" src/services/Merchant.Api/Domains
```
Beklenen: her klasör en fazla bir dosya (yalnız `Domains/Merchants/Merchant.cs`) — `SubMerchants`
artık listede yok.

## Doğrulama S3 — `SubMerchantType` korunur + hizalı (SC-004)

```bash
grep -A4 "enum SubMerchantType" src/services/Merchant.Api/Provider/Onboarding/SubMerchantType.cs
```
Beklenen: `PERSONAL`, `PRIVATE_COMPANY`, `LIMITED_OR_JOINT_STOCK_COMPANY` (3 değer korunur).
`MerchantType` (Personal/PrivateCompany/LimitedOrJointStockCompany) ile birebir eşleşir.

## Doğrulama S4 — Davranış eklenmedi (SC-005)

```bash
git diff --stat master -- src/services/Merchant.Api/Domains/Merchants/
```
Beklenen: `Merchants/` (aggregate + slice'lar) davranışsal değişiklik YOK; yeni endpoint/handler
eklenmedi. (GlobalUsings dışında Merchant domain'i dokunulmadı.)

## Doğrulama S5 — Derleme + mevcut testler yeşil (SC-003, FR-006)

```bash
dotnet build src/services/Merchant.Api/Merchant.Api.csproj
dotnet test tests/Merchant.Api.Tests/Merchant.Api.Tests.csproj
```
Beklenen: build **0 hata**; testler **yeşil** (mevcut test sayısı korunur — `Merchant.SubMerchantKey`
null assert'leri dahil hiçbiri kırılmaz).

## Başarı ölçütleri eşlemesi

| Doğrulama | SC |
|-----------|-----|
| S1 | SC-001 (+FR-001/002/007) |
| S2 | SC-002 (+FR-007) |
| S3 | SC-004 (+FR-005) |
| S4 | SC-005 (+FR-004) |
| S5 | SC-003 (+FR-006) |

> Not: davranış (canlı iyzico kaydı) BU İŞTE YOK — quickstart yapısal doğrulamadır. `Merchant→
> SubMerchant` çeviri + kayıt akışı ayrı davranış spec'inde canlı doğrulanır.
