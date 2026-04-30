# Centralized Error Codes Design

## Summary

Magic string error codes (`"CommissionRate.OutOfRange"` gibi) bounded context başına tek bir static class'ta toplanır. Mevcut tüm kullanımlar refactor edilir.

## Dosya Yapısı

Her bounded context root'una bir `XxxErrors.cs` eklenir:

```
PaymentGatewayApi/Modules/
  BankIntegration/           BankIntegrationErrors.cs
  CommissionManagement/      CommissionErrors.cs
  IAM/                       IamErrors.cs
  MerchantManagement/        MerchantManagementErrors.cs
  PaymentProcessing/         PaymentProcessingErrors.cs
  Settlement/                SettlementErrors.cs
```

## Yapı

Her dosya `public static class` içinde `public const string` olarak tanımlanır. Nested class kullanılmaz.

Const isimleri: `DomainObject` + `Reason` birleşik (örn. `RateOutOfRange`, `BalanceCurrencyMismatch`).  
String değerleri değişmez — API response'larda kullanılıyor olabilir.

### BankIntegrationErrors.cs
```csharp
public static class BankIntegrationErrors
{
    public const string BinRangeInvalidStart      = "BinRange.InvalidStart";
    public const string BinRangeInvalidEnd        = "BinRange.InvalidEnd";
    public const string BinRangeStartAfterEnd     = "BinRange.StartAfterEnd";
    public const string BinCardInfoCardBrandEmpty      = "BinCardInfo.CardBrandEmpty";
    public const string BinCardInfoCardTypeEmpty       = "BinCardInfo.CardTypeEmpty";
    public const string BinCardInfoIssuingCountryEmpty = "BinCardInfo.IssuingCountryEmpty";
}
```

### CommissionErrors.cs
```csharp
public static class CommissionErrors
{
    public const string RateOutOfRange = "CommissionRate.OutOfRange";
}
```

### IamErrors.cs
```csharp
public static class IamErrors
{
    public const string RoleNameEmpty   = "RoleName.Empty";
    public const string RoleNameTooLong = "RoleName.TooLong";
    public const string FullNameFirstNameEmpty = "FullName.FirstNameEmpty";
    public const string FullNameLastNameEmpty  = "FullName.LastNameEmpty";
}
```

### MerchantManagementErrors.cs
```csharp
public static class MerchantManagementErrors
{
    public const string MerchantNameEmpty   = "MerchantName.Empty";
    public const string MerchantNameTooLong = "MerchantName.TooLong";
    public const string ContactInfoInvalidEmail = "ContactInfo.InvalidEmail";
    public const string ContactInfoPhoneEmpty   = "ContactInfo.PhoneEmpty";
    public const string MerchantAddressInvalidCountry = "MerchantAddress.InvalidCountry";
    public const string MerchantAddressCityEmpty      = "MerchantAddress.CityEmpty";
    public const string CurrencyInvalidFormat = "Currency.InvalidFormat";
    public const string CurrencyUnsupported   = "Currency.Unsupported";
    public const string MerchantBankAccountIbanEmpty  = "MerchantBankAccount.IbanEmpty";
    public const string MerchantBankAccountSwiftEmpty = "MerchantBankAccount.SwiftEmpty";
    public const string MerchantCurrencyAlreadySupported = "MerchantCurrency.AlreadySupported";
    public const string MerchantCurrencyNotSupported     = "MerchantCurrency.NotSupported";
    public const string MerchantCurrencyUsedByBankAccount = "MerchantCurrency.UsedByBankAccount";
    public const string MerchantMaxApiKeysReached = "Merchant.MaxApiKeysReached";
}
```

### PaymentProcessingErrors.cs
```csharp
public static class PaymentProcessingErrors
{
    public const string OrderIdEmpty   = "OrderId.Empty";
    public const string OrderIdTooLong = "OrderId.TooLong";
    public const string CardInfoCardNumberEmpty    = "CardInfo.CardNumberEmpty";
    public const string CardInfoCardHolderNameEmpty = "CardInfo.CardHolderNameEmpty";
    public const string CardInfoInvalidIpAddress  = "CardInfo.InvalidIpAddress";
    public const string CommissionInfoNegativeRates           = "CommissionInfo.NegativeRates";
    public const string CommissionInfoMerchantRateBelowBankRate = "CommissionInfo.MerchantRateBelowBankRate";
    public const string BankRoutingInfoBankIdEmpty      = "BankRoutingInfo.BankIdEmpty";
    public const string BankRoutingInfoMerchantCodeEmpty = "BankRoutingInfo.MerchantCodeEmpty";
    public const string BankRoutingInfoTerminalCodeEmpty = "BankRoutingInfo.TerminalCodeEmpty";
    public const string TransactionCommissionRequired = "Transaction.CommissionRequired";
    public const string TransactionCannotFail        = "Transaction.CannotFail";
    public const string TransactionNotApproved       = "Transaction.NotApproved";
    public const string TransactionAlreadySettled    = "Transaction.AlreadySettled";
    public const string TransactionInvalidState      = "Transaction.InvalidState";
}
```

### SettlementErrors.cs
```csharp
public static class SettlementErrors
{
    public const string SettlementCannotMarkProcessing = "Settlement.CannotMarkProcessing";
    public const string SettlementNotProcessing        = "Settlement.NotProcessing";
    public const string SettlementCurrencyMismatch     = "Settlement.CurrencyMismatch";
    public const string SettlementCannotComplete       = "Settlement.CannotComplete";
    public const string SettlementNoLines              = "Settlement.NoLines";
    public const string SettlementAlreadyCompleted     = "Settlement.AlreadyCompleted";
    public const string BalanceCurrencyMismatch        = "MerchantBalance.CurrencyMismatch";
    public const string BalanceInvalidCreditAmount     = "MerchantBalance.InvalidCreditAmount";
    public const string BalanceInvalidDebitAmount      = "MerchantBalance.InvalidDebitAmount";
    public const string BalanceInsufficientBalance     = "MerchantBalance.InsufficientBalance";
    public const string BalancePendingWithdrawalExists = "MerchantBalance.PendingWithdrawalExists";
    public const string WithdrawalRequestNotFound      = "WithdrawalRequest.NotFound";
    public const string WithdrawalRequestIbanEmpty     = "WithdrawalRequest.IbanEmpty";
    public const string WithdrawalRequestCannotApprove = "WithdrawalRequest.CannotApprove";
    public const string WithdrawalRequestCannotReject  = "WithdrawalRequest.CannotReject";
    public const string WithdrawalRequestCannotProcess = "WithdrawalRequest.CannotProcess";
    public const string SettlementPeriodStartAfterEnd  = "SettlementPeriod.StartAfterEnd";
}
```

## Refactor Kapsamı

Etkilenen 14 dosya — mevcut tüm inline string'ler ilgili Errors sınıfına yönlendirilir:

| Bounded Context | Dosya |
|---|---|
| BankIntegration | `BinRecords/ValueObjects/BinRecordValueObjects.cs` |
| CommissionManagement | `BankCommissions/ValueObjects/BankCommissionValueObjects.cs` |
| IAM | `Roles/ValueObjects/RoleName.cs`, `Users/ValueObjects/FullName.cs` |
| MerchantManagement | `Merchants/ValueObjects/MerchantValueObjects.cs`, `Merchants/Entities/MerchantEntities.cs`, `Merchants/Merchant.cs` |
| PaymentProcessing | `PaymentTransactions/ValueObjects/PaymentValueObjects.cs`, `PaymentTransactions/PaymentTransaction.cs` |
| Settlement | `Settlements/ValueObjects/SettlementValueObjects.cs`, `Settlements/Settlement.cs`, `MerchantBalances/MerchantBalance.cs`, `MerchantBalances/Entities/MerchantBalanceEntities.cs` |

## Kapsam Dışı

- `Common/` altında global bir errors sınıfı oluşturmak
- String değerlerini değiştirmek
- Feature handler'lardaki `"NotFound"` benzeri tek seferlik kodlar (bunlar zaten az, bounded context errors sınıfına taşınabilir istenirse)