# Quickstart: TransactionReports Yapısal DDD Geçişi (026)

Davranış yok → doğrulama grep + build + test. Detay: [data-model.md](./data-model.md).

## S1 — Sağlayıcı tipleri `Domains/`'den çıktı (SC-001)

```bash
grep -rlE "class .*: *(BaseRequestV2|ProviderResourceV2)" src/services/Commission.Api/Domains
```
Beklenen: **çıktı yok** (Payouts hariç — Payouts ayrı geçiş, hâlâ Domains'te olabilir; bu komut
Payouts tiplerini de listeleyebilir → yalnız TransactionReports tiplerinin çıktığını teyit et).

```bash
test -d src/services/Commission.Api/Domains/TransactionReports && echo "HALA VAR (HATA)" || echo "klasor dagitildi (OK)"
ls src/services/Commission.Api/Provider/Reporting/ | wc -l   # 13 beklenir
```

## S2 — Aggregate-klasör kuralı (SC-002)

```bash
grep -rlE "class .*: AggregateRoot" src/services/Commission.Api/Domains
```
Beklenen: `TransactionReports` listede YOK (yalnız `CommissionPolicies/CommissionPolicy.cs`).

## S3 — 024 domain dokunulmadı (SC-005)

```bash
git diff --stat -- src/services/Commission.Api/Domains/CommissionPolicies/
```
Beklenen: **boş** (024 `CommissionPolicy` diff yok; yeni endpoint/iş-kuralı eklenmedi).

## S4 — Derleme + testler yeşil (SC-003, FR-006)

```bash
dotnet build src/services/Commission.Api/Commission.Api.csproj
dotnet test tests/Commission.Api.Tests/Commission.Api.Tests.csproj
```
Beklenen: build **0 hata**; testler **20/20 yeşil**.

## Başarı ölçütleri eşlemesi

| Doğrulama | SC |
|-----------|-----|
| S1 | SC-001, SC-004 (+FR-001/002/007) |
| S2 | SC-002 (+FR-003/007) |
| S3 | SC-005 (+FR-004/005) |
| S4 | SC-003 (+FR-006) |

> Davranış (canlı rapor çekimi + 024'e gerçek maliyet) BU İŞTE YOK — ayrı davranış spec'inde canlı
> doğrulanır. Payouts geçişi de ayrı.
