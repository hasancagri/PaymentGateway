# Research: Iyzico Payment Channel — Yapısal Eritme (022)

**Date**: 2026-08-13 | **Spec**: [spec.md](spec.md)

## R1 — Mevcut durum (kod keşfi)

- Kullanıcı üç BC'nin `Domains/` içeriğini elle sildi (staged): Payment (BinCards,
  PaymentSessions, PosAccounts, StoredCards — 30 dosya), Commission (Banks,
  BankCommissions, CommissionDrafts, CommissionProposals, MerchantCommissions,
  SharedKernel — 38 dosya), Merchant (021'den kalan 3 aggregate). Program.cs'ler,
  GlobalUsings'ler ve BC testleri şu an bu tiplere referanslı — çözüm KIRIK.
- `src/services/Iyzipay`: kök çekirdek (Options, HttpClient, RestHttpClientV2,
  HashGeneratorV2, DigestHelper, JsonBuilder, RequestFormatter, StringHelper,
  ToStringRequestBuilder, RequestStringConvertible, IyzipayConstants, BaseRequestV2,
  IyzipayResourceV2, PagingRequest, LoyaltyReward) + `Model/` (76 dosya, + `V2/`) +
  `Request/` (+ `V2/`). iCloud " 2" kopyaları yine türemiş — implement süpürür.
- `Payment.Api/CardVault/` duruyor (DevPanProtector, ICardVault, IPanProtector, PanTools,
  SimulatedCardVault) — 008 `CardInfo`/BinCard tiplerine referansı varsa onarılır.
- Admin ve agent'lar typed client/kendi DTO'larıyla derlenir — dokunulmaz (ölü ekran
  kabulü 021 emsali).

## R2 — Dağıtım haritası (SDK → BC)

**Decision**: SDK yapıları sorumluluk alanına göre üç BC'nin `Domains/`'ine dağıtılır;
namespace'ler BC'ye ait olur, "Iyzipay" namespace/proje adı yaşamaz.

| Hedef | İçerik |
|-------|--------|
| `Payment.Api/Domains/Payments/` | Payment, PaymentResource, PaymentCard, Buyer, Address, BasketItem(+Type), PaymentItem, Currency, Locale, PaymentChannel, PaymentGroup, Status, PaymentPreAuth/PostAuth, Cancel, Refund(+Reason, +ChargedFromMerchant), ConvertedPayout; Requests: CreatePaymentRequest, RetrievePaymentRequest, CreatePaymentPostAuthRequest, CreateCancelRequest, CreateRefundRequest, CreateAmountBasedRefundRequest, UpdatePaymentItemRequest |
| `Payment.Api/Domains/Installments/` | InstallmentInfo, InstallmentDetail, InstallmentPrice, BinNumber; RetrieveInstallmentInfoRequest, RetrieveBinNumberRequest |
| `Payment.Api/Domains/StoredCards/` | Card, CardList, CardInformation, InitialConsumer; CreateCardRequest, DeleteCardRequest, RetrieveCardListRequest (CardVault klasörü aynen kalır) |
| `Merchant.Api/Domains/SubMerchants/` | SubMerchant, SubMerchantType; CreateSubMerchantRequest, UpdateSubMerchantRequest, RetrieveSubMerchantRequest |
| `Commission.Api/Domains/Payouts/` | PayoutCompletedTransaction(+List), BouncedBankTransferList, CrossBookingFromSubMerchant, CrossBookingToSubMerchant; RetrievePayoutTransactionsRequest, RetrieveTransactionsRequest, CreateCrossBookingRequest |
| `Commission.Api/Domains/TransactionReports/` | Model/V2/Transaction + Request/V2 rapor istekleri (TransactionReport*, TransactionDetail*, Retrieve*ReportRequest) + V2 zarf tipleri (ResponseData, ResponsePagingData) |

**Rationale**: Kullanıcı talimatı ("yapıları buralara ekleyeceğiz" — boşaltılmış
Domains'ler) + 023/024'ün hammaddesi doğru BC'de dursun: SubMerchant Merchant
onboarding'inin, payout/rapor Commission'ın konusu.

**Alternatives considered**: Hepsi Payment'ta tek adaptör — 023/024'te yeniden taşıma
gerektirir; kullanıcı çok-BC dağıtımı açıkça istedi ("farklı projelere aktarılabilir").

## R3 — Silinen SDK modülleri

**Decision**: Kullanılmayan yüzey SİLİNİR (git history'de durur): Subscription (tamamı),
IyziLink/FastLink, Apm, Bkm (Basic dahil), CheckoutForm, Iyziup, PayWithIyzico, UCS/
InitUcs, Loyalty (LoyaltyReward, LoyaltyInquiry), CardBlacklist, CardManagementPage,
**tüm Threeds/3D tipleri** (Basic* dahil — 3D sonraki iş), BankTransfer, Approval/
Disapproval, C2C SubMerchant (CreateC2C/VerifyC2C), ProductBuyerInfo, IyziFastLink/
IyziLink istekleri, `Iyzipay.Samples` + `Iyzipay.Tests` + `Iyzipay.Tests.Functional`
projeleri ve SDK README'si.

**Rationale**: Spec FR-003 "yalnız kullanılan işlemler"; 3D bilinçli erteleme. Basic*
setleri eski (pazaryeri-dışı) API yüzeyi — DropShop SubMerchant modeline gitmiyor.

## R4 — Çekirdek istemci altyapısının yeri: BC başına kopya

**Decision**: Çekirdek istemci dosyaları (Options, HttpClient, RestHttpClientV2,
HashGeneratorV2, DigestHelper, JsonBuilder, RequestFormatter, RequestStringConvertible,
StringHelper, ToStringRequestBuilder, IyzipayConstants→sabitler, BaseRequestV2/
IyzipayResourceV2/PagingRequest gerektiği yerde) her BC'de **kendi kopyası** olarak
`<Bc>/Provider/` klasöründe yaşar (Domains DIŞI — Payment.Api `CardVault/` emsali).
Namespace: `<Bc>.Provider`.

**Rationale**: BC izolasyonu (Anayasa I) — BC'ler arası paylaşılan sağlayıcı kütüphanesi
yeni bir "ada" yaratırdı (022'nin sökmeye çalıştığı şey). Kod tekrarı bilinçli kabul —
proje kültürü (015 inline kuralları, Agent slice tekrarları). Common'a koymak tüm
BC'lere + Admin/agent'lara sağlayıcı tipini açar (FR-004 ihlali riski).

**Alternatives considered**: (a) Common'da tek kopya — sınır sızması, Anayasa I gerginliği.
(b) Yalnız Payment'ta olup diğerlerinin HTTP ile Payment'a sorması — bu spec'te akış yok,
yapısal faz; 023/024 karar verir.

## R5 — Aggregate-klasör kuralıyla ilişki (bilinçli ara durum)

**Decision**: Taşınan SDK sınıfları aggregate DEĞİL (davranışsız model/istek tipleri);
`Domains/<Alan>/` klasörleri bu fazda **AggregateRoot içermez**. CLAUDE.md "her Domains
klasörü tek AggregateRoot içerir" kuralı bu ara durumda askıya alınır; 023/024 bu
malzemeden gerçek aggregate'leri şekillendirirken kural yeniden sağlanır. Complexity
Tracking'de gerekçeli.

## R6 — Program.cs onarımları ve testler

**Decision**:
- 3 BC Program.cs: silinen tiplere ait Marten şema kayıtları, endpoint map'leri, MCP
  server/`MapMcp` kayıtları (Payment + Commission), seeder kayıtları (BinCardSeeder) ve
  GlobalUsings satırları temizlenir. Wolverine publish kayıtları Shared event tiplerini
  gösterir — derlenir, KALIR (`merchant.lifecycle`, `merchant.commission`, `mail.delivery`).
- `Merchant.Api/ReadModels/MerchantCommissionGridReadyHandler` Merchant aggregate'ine
  referanslıysa SİLİNİR (aggregate yok).
- `CardVault/`: BinCard/`CardInfo` referansı kırıksa tip vault-içi minimal tanıma çekilir
  veya ilgili üye sadeleştirilir (PAN koruma çekirdeği korunur — FR-006).
- Testler: `tests/Payment.Api.Tests`, `tests/Merchant.Api.Tests`,
  `tests/Commission.Api.Tests` (ölü aggregate testleri) + `tests/Iyzipay.Tests`,
  `tests/Iyzipay.Tests.Functional` SİLİNİR; slnx temizlenir. tests/ boş kalır — 023+
  yeni domain testleri getirir. CLAUDE.md Komutlar bölümü güncellenir.

## R7 — Yapılandırma iskeleti

**Decision**: Her BC'de `Options/` altına strongly-typed sağlayıcı ayar POCO'su
(`PaymentProviderOption` benzeri: ApiKey, SecretKey, BaseUrl) `AddOptionsExt` deseniyle
BAĞLANMAZ bu fazda — akış çalıştırılmadığından yalnız SDK `Options` sınıfı Provider
kopyasında durur; config kablolaması akış işine (sonraki spec) kalır. Depoya sır girmez.

**Rationale**: YAGNI — bağlanmamış options kaydı ölü konfig üretir; FR-007 "tanımlanabilir"
der, zorunlu kılmaz.

## R8 — Söküm listesi (legacy)

**Decision**: `src/services/CP.VPOS` klasörü + slnx girdisi + `.gitignore` satırı
(`src/services/CP.VPOS/`) + `Payment.Api.csproj` referansı silinir. gitignore'daki
`src/reference-architecture.md`/`scenario.md` satırları ve slnx'teki dosya girdileri
dokunulmaz (alakasız). Kalıntı taraması: `CP.VPOS|CPVPOS|BankRouter|PosAccount|BinCard|
Iyzipay` (spec artefaktları + git history hariç) 0 sonuç.