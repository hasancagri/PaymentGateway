# Data Model: Agent-Bazlı Merchant Onboarding Dirilişi (029)

**Date**: 2026-08-14 | **Spec**: [spec.md](spec.md) | **Research**: [research.md](research.md)

## RegisterRequest (YENİ aggregate — Merchant.Api, `Domains/RegisterRequests/`)

`RegisterRequest : AggregateRoot` — merchant adayının başvurusu; tarihçe silinmez (soft delete de yok).

### Alanlar

| Alan | Tip | Not |
|---|---|---|
| `Type` | `MerchantType` | 023 enum'u (Personal/PrivateCompany/LimitedOrJointStockCompany) — paylaşım değil, aynı BC içi |
| `Name` | `string` | zorunlu |
| `Email` | `string` | zorunlu, e-posta biçimi; Trim'lenmiş saklanır; eşleşmeler case-insensitive |
| `GsmNumber` | `string` | zorunlu |
| `Address` | `string` | zorunlu |
| `Iban` | `string` | zorunlu, TR IBAN mod-97 (Merchant ile aynı kural, inline kopya) |
| `ContactName` / `ContactSurname` | `string` | zorunlu |
| `IdentityNumber` | `string?` | tip matrisine göre koşullu |
| `TaxOffice` | `string?` | tip matrisine göre koşullu |
| `TaxNumber` | `string?` | tip matrisine göre koşullu |
| `LegalCompanyTitle` | `string?` | tip matrisine göre koşullu |
| `Status` | `RegisterRequestStatus` | `Pending=1, Approved=2, Rejected=3` (yeni düz enum, aggregate klasöründe) |
| `RejectReason` | `string?` | yalnız Rejected'da dolu |
| `MerchantId` | `Guid?` | yalnız Approved'da dolu — onayda doğan Merchant |

`CreatedTime`/`UpdatedTime` `AggregateRoot`'tan gelir (ayrı alan yok — 024 notu).

### Tip-uyum matrisi (Merchant.Create ile birebir, bilinçli kopya — R6)

| Type | Zorunlu ek alanlar |
|---|---|
| Personal | IdentityNumber |
| PrivateCompany | IdentityNumber + TaxOffice + LegalCompanyTitle |
| LimitedOrJointStockCompany | TaxOffice + TaxNumber + LegalCompanyTitle |

### Davranışlar (hepsi ResultDomain sözleşmesi — 014; `<remarks>Handler: …</remarks>` notu zorunlu)

| Metot | İmza | Kural | Handler |
|---|---|---|---|
| `Submit` | `static ResultDomain<RegisterRequest>` | Tüm doğrulama inline: tip parse edilmiş gelir, zorunlu alanlar, e-posta biçimi, IBAN mod-97, tip-uyum matrisi. Başarıda `Status=Pending`. | `SubmitRegistrationForAgent.Handler` |
| `Approve` | `ResultDomain` (param: `Guid merchantId`) | Yalnız `Pending` → `Approved`; `MerchantId` bağlanır. Değilse `INVALID_OPERATION_ERROR`. | `ApproveRegisterRequest.Handler` |
| `Reject` | `ResultDomain` (param: `string reason`) | Yalnız `Pending` → `Rejected`; boş neden `INVALID_VALUE`. | `RejectRegisterRequest.Handler` |

Mükerrer kontrolü (FR-003) aggregate'te DEĞİL handler'dadır (cross-document sorgu — 024
tekil-aktif deseniyle aynı): Submit handler'ı e-posta ile Pending/Approved kayıt arar;
Pending varsa `RECORD_DUPLICATE`, Approved varsa "zaten onaylı" (`INVALID_OPERATION_ERROR`).

## Merchant (MEVCUT — değişiklik YOK)

Onayda `Merchant.Create(...)` ile doğar (Active, `mk_`+Guid MerchantKey). Bu özellik Merchant
aggregate'ine alan/metot EKLEMEZ. (Not: `GetMerchantResponse.MerchantKey` alanı bu branch'te
zaten dev-açık eklendi — 029'dan bağımsız, korunur.)

## Statü makinesi

```
Submit ──► Pending ──Approve(merchantId)──► Approved   (terminal)
              └──────Reject(reason)───────► Rejected   (terminal; aynı e-posta yeniden başvurabilir)
```

İkinci karar denemesi (Approved/Rejected üstüne) → `INVALID_OPERATION_ERROR`, durum korunur (FR-007).

## Marten kaydı

`opts.Schema.For<RegisterRequest>()` — merchantDb, mevcut şema. Migration yok (dev aşaması,
temiz başlangıç — spec Assumptions).

## Slice haritası (plan referansı)

```
Domains/RegisterRequests/
├── RegisterRequest.cs                     # aggregate
├── RegisterRequestStatus.cs               # enum
├── RegisterRequestMcpTools.cs             # submit_registration + registration_status (yalnız Agents slice'larını bus ile çağırır)
├── RegisterRequestEndpointExtension.cs    # admin uçları map
└── Features/
    ├── Agents/
    │   ├── SubmitRegistrationForAgent.cs      # mükerrer kontrol + Submit + Store
    │   └── RegistrationStatusForAgent.cs      # en-son kayıt; Approved'da Merchant'tan Id+Key okur
    ├── Commands/
    │   ├── ApproveRegisterRequest.cs          # Merchant.Create + Store + MerchantCreated publish + Approve
    │   └── RejectRegisterRequest.cs           # Reject(reason)
    └── Queries/
        └── ListRegisterRequests.cs            # AdminPlaneOnly liste (tarihçe dahil)
```
