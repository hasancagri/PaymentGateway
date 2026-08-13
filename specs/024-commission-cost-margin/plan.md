# Implementation Plan: Commission Cost + Margin

**Branch**: `024-commission-cost-margin` | **Date**: 2026-08-13 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/024-commission-cost-margin/spec.md`

## Summary

Commission BC'nin gerçek domain'i kuruluyor. Bir `CommissionPolicy` aggregate'i merchant başına
gateway marjını (yüzde oran + sabit ücret) tutar; verili bir işlem bağlamı için efektif komisyonu
hesaplar: **efektif komisyon = iyzico maliyeti (`IyzicoCommission` + `IyzicoFee`, işlem-sonrası
rapordan GİRDİ olarak gelir) + gateway marjı (PaidPrice·oran + sabit ücret)**; merchant net hakediş
= PaidPrice − efektif komisyon. Teknik yaklaşım 023 Merchant BC iskeletini birebir izler: tek
aggregate + vertical slice'lar (CQRS), Marten `IDocumentSession`, `ResultDomain`/
`FeatureObjectResultModel`, OpenIddict scope + `AdminPlaneOnly`/`MerchantScoped` politikaları. Saf
domain aritmetiği — iyzico'ya canlı çağrı yok; `Provider/` + `Payouts`/`TransactionReports` iyzico
tipleri uyumaya devam eder (maliyet raporundan çekmek ayrı iş).

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable`, `ImplicitUsings` açık)

**Primary Dependencies**: Marten (Postgres document store), Wolverine (in-proc bus), OpenIddict
(JWT bearer, Identity.Server JWKS), Asp.Versioning (URL segment), Aspire ServiceDefaults

**Storage**: Postgres — Marten document store, `commission` şeması (`SchemaConstants.CommissionSchemaName`);
`commissionDb` connection-string Aspire'dan gelir. `CommissionPolicy` document olarak saklanır.

**Testing**: xUnit — yeni `tests/Commission.Api.Tests` (saf domain birim testi, DB/ağ yok; 023
`Merchant.Api.Tests` desenini izler). `dotnet test` yeşil tutulur.

**Target Platform**: Linux/macOS server — AppHost (Aspire) üzerinden ayağa kalkar

**Project Type**: web-service (mikroservis = tek Bounded Context, mevcut `src/services/Commission.Api`)

**Performance Goals**: Hesaplama saf in-memory decimal aritmetiği; hedef < 5 ms/hesap (I/O yok).
CRUD uçları tek document okuma/yazma.

**Constraints**: Yalnız TL (decimal TRY; çok-para YOK). iyzico'ya canlı çağrı YOK. `Provider/`
sağlayıcı tipleri BC sınırını GEÇMEZ. Deterministik yuvarlama (kuruş, 2 ondalık,
`MidpointRounding.AwayFromZero`).

**Scale/Scope**: Merchant başına EN FAZLA 1 aktif politika. 1 aggregate + 1 VO (MarginRule) + 1
hesap-sonucu VO (EffectiveCommission) + 6 slice + 1 endpoint-extension + 1 status enum.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden kontrol.*

| İlke | Durum | Not |
|------|-------|-----|
| I. BC İzolasyonu | PASS | Commission.Api kendi DB/şeması (`commission`). Merchant/Payment DB'sine erişim YOK. iyzico maliyeti çağrı-GİRDİSİ olarak gelir (Payment/iyzico DB'sine uzanmaz). MerchantId dış referans — cross-BC doğrulama yapılmaz (izolasyon). |
| II. Zengin Domain | PASS | `CommissionPolicy : AggregateRoot` — private setter + `Create` fabrikası + davranış (UpdateMargin/Activate/Deactivate/CalculateEffectiveCommission). `MarginRule` VO (private ctor + `Create`). Sapma: statü düz `enum` (Enumeration değil) — 2026-08-11 refactor + anayasa PATCH amendment beklentisi (araştırma R7); mevcut kod deseni. |
| III. Vertical Slice + CQRS | PASS | `Domains/CommissionPolicies/Features/{Commands,Queries}`. Repository YOK — Marten `IDocumentSession`. Mutasyon `[Transactional]`. Minimal API + `*EndpointExtension`. |
| IV. Result Pattern | PASS | Aggregate → `ResultDomain`/`ResultDomain<T>` (void mutator dahil, 014); handler → `FeatureObjectResultModel<T>`. `MessageItem.Code` resource sabiti. |
| V. Merkezi Kimlik + Açık Yetki | PASS | Yazma → `commission.write` + `AdminPlaneOnly`; admin okuma → `commission.read` + `AdminPlaneOnly`; merchant self-okuma → `commission.read` + `MerchantScoped`; hesaplama → `commission.read` + `AdminPlaneOnly` (sistem/admin). Her uç policy'yi açıkça beyan eder. Tenant izolasyonu MerchantScoped ile fail-closed. |
| VI. Spec-Driven | PASS | specify → clarify (tamam) → plan (bu) → tasks → implement. |
| Tech kısıtları | PASS | .NET 10, Marten, Wolverine, CPM (yeni paket yok), TL-only decimal, marker-DI. |

**Violation yok** → Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/024-commission-cost-margin/
├── plan.md              # Bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/           # Phase 1
│   └── commission-policy.http.md
├── checklists/
│   └── requirements.md  # (mevcut, clarify'da 16/16)
└── tasks.md             # /speckit-tasks (bu komut ÜRETMEZ)
```

### Source Code (repository root)

```text
src/services/Commission.Api/
├── Domains/
│   └── CommissionPolicies/                         # YENİ aggregate klasörü (tek : AggregateRoot)
│       ├── CommissionPolicy.cs                      # aggregate
│       ├── CommissionPolicyStatus.cs               # düz enum (Active/Passive)
│       ├── CommissionPolicyEndpointExtension.cs    # grup map
│       ├── ValueObjects/
│       │   ├── MarginRule.cs                        # VO: RatePercent + FixedFee (+ cap doğrulama)
│       │   └── EffectiveCommission.cs              # hesap-sonucu VO (döküm)
│       └── Features/
│           ├── Commands/
│           │   ├── CreateCommissionPolicy.cs        # AdminPlaneOnly + commission.write
│           │   ├── UpdateCommissionPolicyMargin.cs  # AdminPlaneOnly + commission.write
│           │   └── ChangeCommissionPolicyStatus.cs  # AdminPlaneOnly + commission.write
│           └── Queries/
│               ├── CalculateEffectiveCommission.cs  # AdminPlaneOnly + commission.read (POST)
│               ├── GetCommissionPolicy.cs           # MerchantScoped + commission.read (merchant self)
│               └── ListCommissionPolicies.cs        # AdminPlaneOnly + commission.read
├── Domains/{Payouts,TransactionReports}/           # UYUR — dokunulmaz (iyzico hammadde)
├── Provider/                                        # UYUR — dokunulmaz
├── Options/                                         # gerekirse cap Options POCO'su
└── Program.cs                                       # endpoint-extension map'i eklenir

tests/Commission.Api.Tests/                          # YENİ (xUnit, saf domain)
├── Commission.Api.Tests.csproj
├── MarginRuleTests.cs
├── CommissionPolicyTests.cs
└── EffectiveCommissionTests.cs
```

**Structure Decision**: web-service / tek BC. Mevcut `src/services/Commission.Api` genişletilir;
yeni `Domains/CommissionPolicies/` aggregate klasörü eklenir (uyuyan `Payouts`/`TransactionReports`/
`Provider` dokunulmaz). Test projesi 023 desenini izleyerek geri gelir (`tests/Commission.Api.Tests`,
`PaymentGateway.slnx`'e eklenir). Slice/klasör/policy düzeni 023 Merchant.Api ile birebir hizalı.

## Complexity Tracking

*Violation yok — boş.*