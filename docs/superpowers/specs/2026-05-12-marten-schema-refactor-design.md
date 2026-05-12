# Marten Schema Refactor Design

## Problem

`SchemaConstants` files were defined in some services but never wired into Marten. All services currently write to the `public` schema.

## Approach

Per-service `SchemaConstants` pattern. Each service owns only its own schema name.

## Changes

### New SchemaConstants files

| Service | Schema Name |
|---|---|
| CommissionManagement.Api | `"commissionManagement"` |
| IAM.Api | `"iam"` |
| PaymentProcessing.Api | `"payment"` |

### Existing file cleanup

- `Settlement.Api/Utils/Constants/SchemaConstants.cs` — remove all foreign schema constants, keep only `SETTLEMENT_SCHEMA_NAME`

### Program.cs changes (6 services)

Add `opts.DatabaseSchemaName = SchemaConstants.<X>;` inside each service's `AddMarten(opts => { ... })` block:

| Service | Constant |
|---|---|
| BankIntegration.Api | `SchemaConstants.BANK_INTEGRATION_SCHEMA_NAME` |
| CommissionManagement.Api | `SchemaConstants.COMMISSION_MANAGEMENT_SCHEMA_NAME` |
| IAM.Api | `SchemaConstants.IAM_SCHEMA_NAME` |
| MerchantManagement.Api | `SchemaConstants.MERCHANT_MANAGEMENT_SCHEMA_NAME` |
| PaymentProcessing.Api | `SchemaConstants.PAYMENT_SCHEMA_NAME` |
| Settlement.Api | `SchemaConstants.SETTLEMENT_SCHEMA_NAME` |