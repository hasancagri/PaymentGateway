# Research: Reference.Api Removal (021)

**Date**: 2026-08-13 | **Spec**: [spec.md](spec.md)

## R1 — Bağ haritası (kod keşfi, 2026-08-13)

Reference.Api = HTTP'siz, event-only BC (010): `Domains/{Banks,Countries,Cities,Mccs}` +
`Seeding/{ReferenceSeeder,ReferenceStartupPublisher}`; kendi `referenceDb`'si. Yayın:
`IntegrationEvents.ReferenceDataUpdated(Kind, Items)` → fanout exchange
`reference.data-updated` (`RabbitMqConstants.ReferenceDataUpdated`).

Tüketiciler ve kullanım noktaları:

| Nokta | Kullanım |
|-------|----------|
| `Merchant.Api/ReadModels/ReferenceReadModels.cs` | 4 read-model (Country/City/Mcc/Bank) + `ReferenceEventHandler` + `ReferenceKey` |
| `Merchant.Api/Program.cs` (23-27, 38-48) | Marten Identity(Code) şemaları + exchange declare/bind + durable queue dinleme |
| `CreateSettlementAccount` / `UpdateSettlementAccount` | `ReferenceBank` varlık doğrulaması (yoksa NotFound) |
| `GetSettlementAccounts` / `GetSettlementAccount` | banka adı zenginleştirme (`LoadMany/Load<ReferenceBank>`) |
| `GetMerchant` / `GetMerchantByKey` / `GetMerchantForAgent` | Country/City/Mcc AD zenginleştirme |
| `Commission.Api/ReadModels/ReferenceBankReadModel.cs` | `ReferenceBank` + handler (yalnız Kind=="Bank") |
| `Commission.Api/Program.cs` (28-29, 40-48) | şema + exchange/queue |
| `CreateBank` | katalogdan AD türetme; katalogda yoksa Create reddi |
| `GetBankCatalog` | Admin'e seçilebilir banka listesi (Code+Name, `onlyAvailable`) |
| `SubmitCommissionProposalForAgent` | banka adlarını `ReferenceBank`'ten okuma |
| `Admin/Pages/Banks/Create` | katalog dropdown'u (`GetBankCatalogAsync(onlyAvailable:true)`) |
| `Admin/Pages/SettlementAccounts/{Create,Edit}` | banka dropdown'u (`GetBankCatalogAsync(false)`, Commission API'den) |
| `AppHost.cs` (17, 39-43) | `referenceDb` + `reference-api` kaydı |

Identity.Server'da `reference` scope/istemcisi YOK (grep boş) — kimlik tarafında iş yok.
SharedKernel yalnız `CardTaxonomy/{CardBrand,CardType}` içerir; Payment + Commission
kullanıyor → KORUNUR (FR-007).

## R2 — Commission banka tanımı: ad kullanıcı girdisine döner

**Decision**: `CreateBankCommand`'e `Name` alanı eklenir; katalog doğrulaması ve
`ReferenceBank` yüklemesi silinir. `Bank.Create(code, name, installments)` imzası zaten
ad alıyor — davranışı değişmez (boş-ad reddi aggregate'te varsa aynen kalır). Kod
benzersizliği kontrolü (Bank sorgusu) aynen korunur.

**Rationale**: Aggregate imzası hazır; tek değişiklik adın kaynağı (katalog → komut).
024 banka eksenini tamamen kaldırana dek en küçük dokunuş.

**Alternatives considered**: Ad alanını atmak (yalnız kod) — Admin listeleri ve 019
teklif satırları banka adı gösteriyor; ad kalmalı.

## R3 — GetBankCatalog SÖKÜLÜR; Admin dropdown'ları serbest girişe döner

**Decision**: `GetBankCatalog` slice + endpoint + Admin `CommissionApiClient.GetBankCatalogAsync`
+ `BankCatalogItem` modeli silinir. `Admin/Pages/Banks/Create` → Code + Name text input.
`Admin/Pages/SettlementAccounts/{Create,Edit}` → BankCode text input (Commission API'ye
cross-BC katalog çağrısı da ortadan kalkar).

**Rationale**: Kataloğun tek kaynağı ReferenceBank read-model'iydi; veri kaynağı ölünce
endpoint anlamsız. Admin'in Commission'a settlement formu için gitmesi (UI-composition)
zaten 021 ile gereksizleşir.

**Alternatives considered**: Endpoint'i Bank aggregate'ten (eklenmiş bankalar) beslemek —
settlement formu "tüm bankalar" ister, Commission'a eklenmişler değil; yanlış anlam.

## R4 — Settlement: katalog doğrulaması kalkar, sorgular ad zenginleştirmesiz

**Decision**: `CreateSettlementAccount`/`UpdateSettlementAccount`'ta `ReferenceBank`
varlık kontrolü (NotFound dalı) silinir; `BankCode` serbest metin olarak saklanır (IBAN
mod-97 + benzersizlik + merchant-varlık kontrolleri AYNEN kalır). `GetSettlementAccounts`/
`GetSettlementAccount` yanıtından `BankName` alanı çıkar (yalnız `BankCode` döner); Admin
listeleri kodu gösterir.

**Rationale**: Kullanıcı kararı (davranış sadeleşmesi kabul). Aggregate `BankName`
saklamıyor — ad her sorguda katalogdan geliyordu; katalog ölünce alanın dürüst hâli yokluk.

**Alternatives considered**: `BankName`'i aggregate'e taşımak — 023 settlement'ı iyzico'ya
devredecek; ölecek yapıya alan eklemek israf.

## R5 — Merchant sorguları: Country/City/MCC ad zenginleştirmesi kalkar

**Decision**: `GetMerchant`/`GetMerchantByKey`/`GetMerchantForAgent` yanıtlarından
`CountryName`/`CityName`/`MccDescription` türü ad alanları çıkar; ham kodlar kalır.
`ReferenceKey` helper'ı read-model dosyasıyla birlikte silinir.

**Rationale**: Merchant profil alanları onboarding'de zaten boş dolduruluyor (bilinen
boşluk — `UpdateMerchantProfile` yok); ad zenginleştirmesi fiilen boş katalogla null
dönüyordu. Kod alanları korunur, API tüketicileri (Admin, agent) kod gösterir.

**Alternatives considered**: Alanları bırakıp hep null döndürmek — ölü sözleşme, yanıltıcı.

## R6 — Teklif akışı banka adları Commission'ın kendi Bank aggregate'inden

**Decision**: `SubmitCommissionProposalForAgent`'taki `ReferenceBank` sorgusu, Commission'ın
kendi `Bank` dokümanlarına (`Code+Name`, silinmemiş) çevrilir.

**Rationale**: Teklif satırları banka grid'inden doğar; grid'deki her banka Commission'da
`Bank` olarak zaten var — ad kaynağı olarak daha doğru (BC-içi, dış kopya değil).

## R7 — Mesajlaşma ve orkestrasyon temizliği

**Decision**: `Shared/IntegrationEvents.cs`'ten `ReferenceDataUpdated` + `ReferenceItem`;
`Shared/RabbitMqConstants.cs`'ten `ReferenceDataUpdated` sınıfı; iki BC Program.cs'inden
exchange declare/bind + durable queue dinlemeleri + Marten şema kayıtları; `AppHost.cs`'ten
`referenceDb` + `reference-api` bloğu silinir. Broker'daki eski kuyruk/exchange kalıntısı
dev sıfırlamasıyla gider (koddan deklarasyon kalkması yeterli — spec varsayımı).

## R8 — Silme envanteri ve doğrulama

**Decision**: `src/services/Reference.Api/` + `tests/Reference.Api.Tests/` klasörleri
kökten silinir; slnx'ten iki proje girdisi çıkar. Doğrulama: çözüm genelinde
case-insensitive `ReferenceDataUpdated|ReferenceBank|ReferenceCountry|ReferenceCity|
ReferenceMcc|Reference.Api|reference-api|referenceDb` taraması 0 sonuç (spec artefaktları
hariç). CLAUDE.md'de Reference.Api'ye dair açık bölüm yok; 010/012 bağlam cümleleri
implement'te gözden geçirilir.