# Quickstart / Doğrulama: Domain Sonuç Sarmalama (PaymentGateway)

Bu refactor davranışı değiştirmez; doğrulama derleyici + mevcut birim testleridir.

## Ön koşul

- .NET 10 SDK
- Repo kökü: `/Users/macbook/Desktop/PaymentGateway`

## Adımlar

1. **Build (tüm çözüm)** — imza değişimi tüm çağıranlarda tutarlı mı:
   ```bash
   dotnet build
   ```
   Beklenen: `0 Hata`. (Sarılmamış tek call-site kalırsa CS derleme hatası verir — istenen kapı.)

2. **Domain birim testleri**:
   ```bash
   dotnet test tests/Merchant.Api.Tests
   dotnet test tests/Commission.Api.Tests
   ```
   Beklenen: 0 başarısız. Merchant testleri güncellenmiş `IsSuccess`/`.Data` desenini kullanır.

3. **Ham-dönüş taraması** (regresyon kapısı) — handler'dan çağrılan hedef metotların artık
   `ResultDomain` döndüğünü doğrula:
   ```bash
   grep -nE "public .*(bool|ChallengeOutcome) (TryActivate|Verify)\(" \
     src/services/Merchant.Api/Domains -r
   ```
   Beklenen: eşleşme yok (hepsi `ResultDomain`/`ResultDomain<T>`).

## Kabul (spec Success Criteria eşlemesi)

| Ölçüt | Doğrulama |
|-------|-----------|
| SC-001 (ham davranış metodu = 0) | Adım 3 grep boş + envanterdeki 5 metot sarıldı |
| SC-002 (build 0 hata) | Adım 1 |
| SC-003 (testler yeşil) | Adım 2 |
| SC-004 (iç içe aggregate = 0) | `Domains/` her klasör tek AggregateRoot (Merchant BC düzeltildi) |
| SC-005 (CLAUDE.md 3 kural) | `CLAUDE.md`'de sonuç-sarmalama + aggregate-klasör + ValueObjects maddeleri |

## Kapsam dışı (dokunulmadığı doğrulanır)

- `PosAccount.GetCommissionRate` ham `decimal?` döner (getter muaf).
- Reference/Commission handler-çağrılı ham davranış metodu: yok (build teyit eder).
