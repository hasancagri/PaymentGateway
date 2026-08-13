# Tasks: Reference.Api Removal

**Input**: Design documents from `/specs/021-reference-api-removal/`

**Kapsam notu (kullanıcı, 2026-08-13)**: Canlı Aspire doğrulaması BU İŞTE YAPILMAZ
("uygulamanın düzgün çalışıp çalışmadığına şu an bakmıyorum — projeler silinsin yeter").
Kapanış ölçütü: silme tamam + `dotnet build` 0 hata + kalan testler yeşil + kalıntı
taraması 0. Quickstart S3-S6 canlı senaryoları ileriye bırakıldı.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

- [X] T001 `master`'dan `021-reference-api-removal` dalını oluştur

## Phase 2: Foundational

*(Görev yok)*

## Phase 3: User Story 1 - Reference.Api sistemden çıkar (P1) 🎯 MVP

- [X] T002 [US1] `src/services/Reference.Api/` ve `tests/Reference.Api.Tests/` klasörlerini sil
- [X] T003 [US1] `PaymentGateway.slnx`'ten iki proje girdisini çıkar
- [X] T004 [US1] `src/aspire/AppHost/AppHost.cs`'ten `referenceDb` tanımı + `reference-api` bloğunu çıkar

## Phase 4: User Story 3 - Sözleşme ve yerel kopya temizliği (P3; US2'den önce — derleme bunları gerektirir)

- [X] T005 [P] [US3] `src/others/Shared/IntegrationEvents.cs`'ten `ReferenceDataUpdated` + `ReferenceItem`; `RabbitMqConstants.cs`'ten `ReferenceDataUpdated` sınıfını çıkar
- [X] T006 [P] [US3] `src/services/Merchant.Api/ReadModels/ReferenceReadModels.cs`'i sil; `Program.cs`'ten 4 Marten şema kaydı + exchange/queue bloğunu çıkar
- [X] T007 [P] [US3] `src/services/Commission.Api/ReadModels/ReferenceBankReadModel.cs`'i sil; `Program.cs`'ten şema kaydı + exchange/queue bloğunu çıkar

## Phase 5: User Story 2 - Katalog-bağımlı akışlar katalogsuz (P2)

- [X] T008 [US2] Merchant settlement: `Create/UpdateSettlementAccount`'tan ReferenceBank kontrolünü, `GetSettlementAccounts/GetSettlementAccount`'tan BankName zenginleştirmesini çıkar
- [X] T009 [P] [US2] Merchant sorguları: `GetMerchant`/`GetMerchantByKey`/`GetMerchantForAgent`'tan Country/City/MCC ad zenginleştirmesini çıkar
- [X] T010 [P] [US2] Commission: `CreateBank`'e `Name` alanı (katalog türetmesi yerine); `GetBankCatalog` slice'ını + endpoint kaydını sil; `SubmitCommissionProposalForAgent` banka adlarını kendi `Bank` dokümanlarından alsın
- [X] T011 [US2] **KAPSAM DEĞİŞTİ (kullanıcı, implement sırasında)**: Admin temizliği YAPILMADI — Merchant.Api'nin TÜM Features/MCP yüzeyi silindiği için (aşağıdaki not) Admin merchant/settlement/banka ekranları zaten fiilen ölü; derleniyorlar, 023'te yeniden bağlanacak

## Phase 6: Polish

- [X] T012 CLAUDE.md'deki Reference izlerini temizle
- [X] T013 Doğrulama: `dotnet build` 0 hata; `dotnet test` yeşil (Reference'sız); kalıntı taraması (quickstart S1) 0 satır; sonucu quickstart Notlar'a işle; commit

## Uygulama notu (2026-08-13)

Implement sırasında kullanıcı talimatı: **"Merchant içerisindeki bütün Feature'ları silebilirsin"** —
021 kapsamı genişledi: Merchant.Api'nin `Domains/*/Features/*` tamamı + endpoint extension'lar +
MCP tool'lar + MCP server kaydı silindi (aggregate'ler, ReadModels handler, lifecycle yayını kaldı).
`ReferenceKeyTests.cs` silindi (Merchant testleri 50→46). Canlı Aspire doğrulaması yapılmadı
(kullanıcı kararı); kapanış = build 0 hata + 201 test yeşil + kalıntı taraması 0.

## Dependencies

T001 → T002-T004 → T005-T007 (paralel) → T008-T011 → T012-T013