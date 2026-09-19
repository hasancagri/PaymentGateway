# Implementation Plan: Hosted Onboarding Form + Tek Kullanımlık Credential Teslimi

**Branch**: `045-hosted-onboarding-form` | **Date**: 2026-09-19 | **Spec**: [spec.md](spec.md)

**Input**: ECom 078 kontratı — [contracts/pg-onboarding-rest.md](contracts/pg-onboarding-rest.md)

## Summary

Merchant.Api'ye üç S2S REST ucu (form oturumu aç / durum sorgusu / credential doğrulama) + iki
hosted sayfa (başvuru formu, tek kullanımlık teslim sayfası) eklenir. Approve anında başvuru
e-postasına teslim linkli mail gider (outbox → Mail.Worker). Yeni akış store ile canlı
doğrulanınca 029'un ECom-yönlü MCP tool'ları (`submit_registration`/`registration_status`)
sökülür — PII ve MerchantKey artık ne MCP yanıtında ne S2S yanıtında.

## Technical Context

**Language/Version**: .NET 10 / C# · **Dependencies**: Marten (merchantDb, schema
`merchantManagement`), Wolverine (outbox + RabbitMQ `mail.delivery`), Minimal API, xUnit+Shouldly

**Storage**: merchantDb — YENİ `OnboardingFormSession` + `CredentialRevealLink`; mevcut
`RegisterRequest`/`Merchant` değişmez

**Auth**: mevcut `ecommerce-onboarding` m2m (merchant.read/write) — Identity seed DEĞİŞMEZ;
sayfalar anonim (token = yetki; Payment.Api 041 hosted sayfa emsali)

**Testing**: saf domain (iki yeni aggregate) test-first; endpoint/HTML canlı doğrulama
(quickstart)

**Target**: Merchant.Api (5202); Mailpit Aspire'da mevcut

## Constitution Check — GEÇTİ

- **İLKE I**: UYUMLU — dış realm (store) kanalı tipli REST kontratı; DB izolasyonu değişmez.
- **İLKE II**: UYUMLU — iki yeni aggregate kimlikli + yaşam döngülü; tek-kullanım invariant'ı
  `Consume`'da; form doğrulaması `RegisterRequest.Submit`'te kalır.
- **İLKE III**: UYUMLU — slice yerleşimi research D6; repository yok.
- **İLKE IV**: UYUMLU — Feature*ResultModel / ResultDomain; kodlar Merchant resource sabitleri.
- **İLKE V**: UYUMLU — S2S uçlar scope'lu (read/write ayrımı kontrattaki gibi); anonim sayfalar
  sunucu-durumlu tek-kullanımlık token'la (041 hosted-ödeme sayfası emsali; sır token'sız
  erişilemez). MerchantKey emisyonu MCP/S2S yanıtından ÇIKAR — İlke V "sır asgari yüzey" yönünde
  iyileşme.
- **İLKE VI (SDD)**: bu artefakt seti; kontrat 078'den devralındı (clarify orada).
- **İLKE VII**: Merchant FLOW.md aynı PR'da güncellenir (başvuru doğuşu form; teslim mail+sayfa).

## Project Structure

```text
src/services/Merchant.Api/
├── Domains/OnboardingFormSessions/
│   ├── OnboardingFormSession.cs                    # YENİ aggregate
│   ├── OnboardingFormSessionEndpointExtension.cs   # POST /api/v1/onboarding/sessions (S2S)
│   │                                               # + GET/POST /onboarding/form/{token} (sayfa)
│   └── Features/Commands/
│       ├── CreateFormSession.cs                    # S2S: Pending-kontrol + oturum üret
│       └── SubmitOnboardingForm.cs                 # form POST → RegisterRequest.Submit + mail
├── Domains/CredentialRevealLinks/
│   ├── CredentialRevealLink.cs                     # YENİ aggregate
│   ├── CredentialRevealLinkEndpointExtension.cs    # GET /onboarding/reveal/{token} (sayfa)
│   └── Features/Agents/Commands/
│       └── AdminResendCredentialLink.cs            # YENİ MCP tool (merchant.admin)
├── Domains/RegisterRequests/Features/Queries/
│   └── GetOnboardingApplicationStatus.cs           # GET /api/v1/onboarding/applications/{email}
├── Domains/Merchants/Features/Queries/
│   └── ValidateMerchantCredentials.cs              # POST /api/v1/onboarding/credentials/validate
├── Domains/RegisterRequests/Features/Agents/Commands/
│   ├── AdminApproveRegistration.cs                 # DEĞİŞİR: reveal link + mail kancası
│   ├── SubmitRegistration.cs                       # SÖKÜM (US4, canlı PASS sonrası)
│   └── (RegistrationStatus query dosyası)          # SÖKÜM (US4)
├── Options/Onboarding.cs                           # DEĞİŞİR: PublicBaseUrl + iki lifetime
├── FLOW.md                                         # DEĞİŞİR (İLKE VII, aynı PR)
tests/Merchant.Api.Tests/
├── OnboardingFormSessionTests.cs                   # YENİ, test-first
└── CredentialRevealLinkTests.cs                    # YENİ, test-first
```

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (yok) — anonim hosted sayfa 041 emsaliyle mevcut desen | — | — |
