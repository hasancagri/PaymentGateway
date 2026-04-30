# Unit Test Projesi Tasarımı — PaymentGateway.Tests

**Tarih:** 2026-04-30  
**Kapsam:** Tüm bounded context'ler için domain unit testleri  
**Onay durumu:** Kullanıcı onayladı

---

## Amaç

PaymentGatewayApi'deki 6 bounded context'in domain entity'leri ve value object'larını veritabanı bağımlılığı olmadan birim testlerle doğrulamak. Ayrı bir `PaymentGateway.Tests` projesi oluşturulur ve solution'a eklenir.

---

## Proje Yapısı

```
PaymentGateway.Tests/
├── PaymentGateway.Tests.csproj
├── GlobalUsings.cs
├── BankIntegration/
│   ├── Banks/
│   │   ├── BankTests.cs
│   │   ├── BankNameTests.cs
│   │   └── BankPriorityTests.cs
│   └── BinRecords/
│       ├── BinRecordTests.cs
│       └── BinRecordValueObjectTests.cs
├── CommissionManagement/
│   ├── BankCommissions/
│   │   ├── BankCommissionTests.cs
│   │   └── BankCommissionValueObjectTests.cs
│   └── MerchantCommissions/
│       ├── MerchantCommissionTests.cs
│       └── MerchantCommissionValueObjectTests.cs
├── IAM/
│   ├── Roles/
│   │   ├── RoleTests.cs
│   │   └── RoleValueObjectTests.cs
│   └── Users/
│       ├── UserTests.cs
│       └── UserValueObjectTests.cs
├── MerchantManagement/
│   └── Merchants/
│       ├── MerchantTests.cs
│       └── MerchantValueObjectTests.cs
├── PaymentProcessing/
│   └── PaymentTransactions/
│       ├── PaymentTransactionTests.cs
│       └── PaymentTransactionValueObjectTests.cs
└── Settlement/
    ├── MerchantBalances/
    │   ├── MerchantBalanceTests.cs
    │   └── MerchantBalanceValueObjectTests.cs
    └── Settlements/
        ├── SettlementTests.cs
        └── SettlementValueObjectTests.cs
```

---

## Bağımlılıklar

### csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.5.1" />
    <PackageReference Include="FluentAssertions" Version="7.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\PaymentGatewayApi\PaymentGatewayApi.csproj" />
  </ItemGroup>
</Project>
```

### GlobalUsings.cs

```csharp
global using Xunit;
global using FluentAssertions;
global using PaymentGatewayApi.Modules.BankIntegration.Banks;
// ... diğer modül using'leri
```

---

## Test Kapsamı

### Entity Testleri (`*Tests.cs`)

Her aggregate root entity için:
- Factory metodları (Configure, Create, Initiate vb.) geçerli input ile başarı döner
- Factory metodları geçersiz input ile hata döner, doğru hata kodu içerir
- State geçiş metodları (Activate/Deactivate, Approve/Decline vb.) doğru state'e geçirir
- İş kuralı ihlalleri (zaten aktif bankaya activate çağrısı vb.) doğru hata döner
- Koleksiyon metodları (AddSupportedCurrency, AddPermission vb.) duplicate engeller

### Value Object Testleri (`*ValueObjectTests.cs`)

Her value object için:
- Geçerli değerle `ResultDomain.IsSuccess == true` ve `Data` dolu
- Boş/null değerle doğru hata kodu döner
- Sınır değerleri (minimum, maksimum) test edilir
- `FromPersistence` fabrikası ile oluşturulan nesne doğru değer taşır

---

## Adlandırma Kuralı

```
MethodName_Senaryo_BeklenenSonuç
```

Örnekler:
```csharp
Configure_WithValidInputs_ReturnsSuccess()
Configure_WithEmptyName_ReturnsErrorWithCorrectCode()
Activate_WhenAlreadyActive_StillSetsActiveStatus()
BankName_WhenEmpty_ReturnsError()
BankPriority_WhenZero_ReturnsError()
BankPriority_WhenPositive_ReturnsSuccess()
AddSupportedCurrency_WhenDuplicate_ReturnsError()
```

---

## Aggregate Root'lar (10 adet)

| Bounded Context       | Entity              |
|-----------------------|---------------------|
| BankIntegration       | Bank, BinRecord     |
| CommissionManagement  | BankCommission, MerchantCommission |
| IAM                   | Role, User          |
| MerchantManagement    | Merchant            |
| PaymentProcessing     | PaymentTransaction  |
| Settlement            | MerchantBalance, Settlement |

---

## Kapsam Dışı

- Command/Query handler testleri (DbContext gerektirir — integration test kapsamı)
- Endpoint testleri
- Wolverine mesaj routing testleri