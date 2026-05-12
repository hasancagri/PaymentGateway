# Marten Schema Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire `DatabaseSchemaName` into every service's Marten setup using per-service `SchemaConstants`, so Marten tables are isolated under named PostgreSQL schemas instead of `public`.

**Architecture:** Each service owns a single `SchemaConstants` class with only its own schema name. The constant is referenced in `Program.cs` via `opts.DatabaseSchemaName`. No cross-service schema knowledge.

**Tech Stack:** .NET 9, Marten, Wolverine, Aspire AppHost

---

## File Map

| Action | Path | Responsibility |
|---|---|---|
| Fix | `BankIntegration.Api/Utils/Constants/SchemaConstants.cs` | Fix namespace; trim to own schema only |
| Create | `CommissionManagement.Api/Utils/Constants/SchemaConstants.cs` | Own schema name |
| Create | `IAM.Api/Utils/Constants/SchemaConstants.cs` | Own schema name |
| Create | `PaymentProcessing.Api/Utils/Constants/SchemaConstants.cs` | Own schema name |
| Fix | `Settlement.Api/Utils/Constants/SchemaConstants.cs` | Trim to own schema only |
| Modify | `BankIntegration.Api/Program.cs` | Add `DatabaseSchemaName` |
| Modify | `CommissionManagement.Api/Program.cs` | Add `DatabaseSchemaName` |
| Modify | `IAM.Api/Program.cs` | Add `DatabaseSchemaName` |
| Modify | `MerchantManagement.Api/Program.cs` | Add `DatabaseSchemaName` |
| Modify | `PaymentProcessing.Api/Program.cs` | Add `DatabaseSchemaName` |
| Modify | `Settlement.Api/Program.cs` | Add `DatabaseSchemaName` |

---

### Task 1: Fix BankIntegration.Api SchemaConstants

**Files:**
- Modify: `BankIntegration.Api/Utils/Constants/SchemaConstants.cs`

Current file has wrong namespace (`PaymentGatewayApi.Utils.Constants`) and contains all services' schema names instead of just its own.

- [ ] **Step 1: Replace file content**

Replace entire file with:

```csharp
namespace BankIntegration.Api.Utils.Constants;

public class SchemaConstants
{
    public static readonly string BANK_INTEGRATION_SCHEMA_NAME = "bankIntegration";
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build BankIntegration.Api/BankIntegration.Api.csproj
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add BankIntegration.Api/Utils/Constants/SchemaConstants.cs
git commit -m "refactor: fix BankIntegration SchemaConstants namespace, trim to own schema"
```

---

### Task 2: Create CommissionManagement.Api SchemaConstants

**Files:**
- Create: `CommissionManagement.Api/Utils/Constants/SchemaConstants.cs`

- [ ] **Step 1: Create file**

```csharp
namespace CommissionManagement.Api.Utils.Constants;

public class SchemaConstants
{
    public static readonly string COMMISSION_MANAGEMENT_SCHEMA_NAME = "commissionManagement";
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build CommissionManagement.Api/CommissionManagement.Api.csproj
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add CommissionManagement.Api/Utils/Constants/SchemaConstants.cs
git commit -m "refactor: add SchemaConstants to CommissionManagement.Api"
```

---

### Task 3: Create IAM.Api SchemaConstants

**Files:**
- Create: `IAM.Api/Utils/Constants/SchemaConstants.cs`

- [ ] **Step 1: Create file**

```csharp
namespace IAM.Api.Utils.Constants;

public class SchemaConstants
{
    public static readonly string IAM_SCHEMA_NAME = "iam";
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build IAM.Api/IAM.Api.csproj
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add IAM.Api/Utils/Constants/SchemaConstants.cs
git commit -m "refactor: add SchemaConstants to IAM.Api"
```

---

### Task 4: Create PaymentProcessing.Api SchemaConstants

**Files:**
- Create: `PaymentProcessing.Api/Utils/Constants/SchemaConstants.cs`

- [ ] **Step 1: Create file**

```csharp
namespace PaymentProcessing.Api.Utils.Constants;

public class SchemaConstants
{
    public static readonly string PAYMENT_SCHEMA_NAME = "payment";
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build PaymentProcessing.Api/PaymentProcessing.Api.csproj
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add PaymentProcessing.Api/Utils/Constants/SchemaConstants.cs
git commit -m "refactor: add SchemaConstants to PaymentProcessing.Api"
```

---

### Task 5: Trim Settlement.Api SchemaConstants

**Files:**
- Modify: `Settlement.Api/Utils/Constants/SchemaConstants.cs`

Current file contains all services' schema names. Only settlement's own name should remain.

- [ ] **Step 1: Replace file content**

```csharp
namespace Settlement.Api.Utils.Constants;

public class SchemaConstants
{
    public static readonly string SETTLEMENT_SCHEMA_NAME = "settlement";
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build Settlement.Api/Settlement.Api.csproj
```

Expected: Build succeeded with 0 errors. (If there were references to removed constants, the build will point them out — remove those references.)

- [ ] **Step 3: Commit**

```bash
git add Settlement.Api/Utils/Constants/SchemaConstants.cs
git commit -m "refactor: trim Settlement.Api SchemaConstants to own schema only"
```

---

### Task 6: Wire DatabaseSchemaName into all Program.cs files

**Files:**
- Modify: `BankIntegration.Api/Program.cs`
- Modify: `CommissionManagement.Api/Program.cs`
- Modify: `IAM.Api/Program.cs`
- Modify: `MerchantManagement.Api/Program.cs`
- Modify: `PaymentProcessing.Api/Program.cs`
- Modify: `Settlement.Api/Program.cs`

In each service, add `opts.DatabaseSchemaName = SchemaConstants.<X>;` as the first line inside the `AddMarten(opts => { ... })` lambda.

- [ ] **Step 1: BankIntegration.Api/Program.cs**

Find the `AddMarten` block and add the schema line:

```csharp
builder.Services.AddMarten(opts =>
{
    opts.DatabaseSchemaName = SchemaConstants.BANK_INTEGRATION_SCHEMA_NAME;
    opts.Connection(bankIntDb);
    // ... rest unchanged
```

Add using at top if not present via GlobalUsings:
```csharp
using BankIntegration.Api.Utils.Constants;
```

- [ ] **Step 2: CommissionManagement.Api/Program.cs**

```csharp
builder.Services.AddMarten(opts =>
{
    opts.DatabaseSchemaName = SchemaConstants.COMMISSION_MANAGEMENT_SCHEMA_NAME;
    opts.Connection(commissionDb);
    // ... rest unchanged
```

Add using:
```csharp
using CommissionManagement.Api.Utils.Constants;
```

- [ ] **Step 3: IAM.Api/Program.cs**

```csharp
builder.Services.AddMarten(opts =>
{
    opts.DatabaseSchemaName = SchemaConstants.IAM_SCHEMA_NAME;
    opts.Connection(iamDb!);
    // ... rest unchanged
```

Add using:
```csharp
using IAM.Api.Utils.Constants;
```

- [ ] **Step 4: MerchantManagement.Api/Program.cs**

```csharp
builder.Services.AddMarten(opts =>
{
    opts.DatabaseSchemaName = SchemaConstants.MERCHANT_MANAGEMENT_SCHEMA_NAME;
    opts.Connection(merchantDb);
    // ... rest unchanged
```

Add using:
```csharp
using MerchantManagement.Api.Utils.Constants;
```

- [ ] **Step 5: PaymentProcessing.Api/Program.cs**

```csharp
builder.Services.AddMarten(opts =>
{
    opts.DatabaseSchemaName = SchemaConstants.PAYMENT_SCHEMA_NAME;
    opts.Connection(paymentDb);
    // ... rest unchanged
```

Add using:
```csharp
using PaymentProcessing.Api.Utils.Constants;
```

- [ ] **Step 6: Settlement.Api/Program.cs**

```csharp
builder.Services.AddMarten(opts =>
{
    opts.DatabaseSchemaName = SchemaConstants.SETTLEMENT_SCHEMA_NAME;
    opts.Connection(settlementDb);
    // ... rest unchanged
```

Add using:
```csharp
using Settlement.Api.Utils.Constants;
```

- [ ] **Step 7: Build entire solution**

```bash
dotnet build PaymentGateway.slnx
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 8: Commit**

```bash
git add BankIntegration.Api/Program.cs CommissionManagement.Api/Program.cs IAM.Api/Program.cs MerchantManagement.Api/Program.cs PaymentProcessing.Api/Program.cs Settlement.Api/Program.cs
git commit -m "refactor: wire DatabaseSchemaName into all Marten configurations"
```