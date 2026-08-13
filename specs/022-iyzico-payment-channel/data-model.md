# Data Model: Iyzico Payment Channel — Yapısal Eritme (022)

**Date**: 2026-08-13 | **Plan**: [plan.md](plan.md)

Aggregate/kalıcı model YOK (bilinçli — plan Complexity Tracking). "Model" = dosya dağıtım
eşlemesi. Tüm taşınan dosyalarda namespace BC'ye göre değişir; sınıf adları korunur
("Iyzipay" yalnız proje/namespace düzeyinde ölür).

## Payment.Api

| Hedef | Kaynak (src/services/Iyzipay) |
|-------|-------------------------------|
| `Provider/` | Options, HttpClient, RestHttpClientV2, HashGeneratorV2, DigestHelper, JsonBuilder, RequestFormatter, RequestStringConvertible, StringHelper, ToStringRequestBuilder, IyzipayConstants (+ v1 taban tipleri: BaseRequest/IyzipayResource — Model kökünde neredeyse oradan) |
| `Domains/Payments/` | Model: Payment, PaymentResource, PaymentCard, Buyer, Address, BasketItem, BasketItemType, PaymentItem, Currency, Locale, PaymentChannel, PaymentGroup, Status, PaymentPreAuth, PaymentPostAuth, Cancel, Refund, RefundReason, RefundChargedFromMerchant, ConvertedPayout. Request: CreatePaymentRequest, RetrievePaymentRequest, CreatePaymentPostAuthRequest, CreateCancelRequest, CreateRefundRequest, CreateAmountBasedRefundRequest, UpdatePaymentItemRequest |
| `Domains/Installments/` | Model: InstallmentInfo, InstallmentDetail, InstallmentPrice, BinNumber. Request: RetrieveInstallmentInfoRequest, RetrieveBinNumberRequest |
| `Domains/StoredCards/` | Model: Card, CardList, CardInformation, InitialConsumer. Request: CreateCardRequest, DeleteCardRequest, RetrieveCardListRequest |

`CardVault/` DEĞİŞMEZ (kırık referans onarımı hariç).

## Merchant.Api

| Hedef | Kaynak |
|-------|--------|
| `Provider/` | Payment.Api `Provider/` setinin kopyası (mini: SubMerchant isteklerinin gerektirdiği çekirdek) |
| `Domains/SubMerchants/` | Model: SubMerchant, SubMerchantType. Request: CreateSubMerchantRequest, UpdateSubMerchantRequest, RetrieveSubMerchantRequest |

## Commission.Api

| Hedef | Kaynak |
|-------|--------|
| `Provider/` | Çekirdek kopya + V2 tabanları: BaseRequestV2, IyzipayResourceV2, PagingRequest, Model/V2/{ResponseData, ResponsePagingData} |
| `Domains/Payouts/` | Model: PayoutCompletedTransaction, PayoutCompletedTransactionList, BouncedBankTransferList, CrossBookingFromSubMerchant, CrossBookingToSubMerchant. Request: RetrievePayoutTransactionsRequest, RetrieveTransactionsRequest, CreateCrossBookingRequest |
| `Domains/TransactionReports/` | Model/V2/Transaction: TransactionReport, TransactionReportItem, TransactionReportResource, TransactionDetail, TransactionDetailItem, TransactionDetailResource, TransactionDetailCancelItem, PaymentTxDetailItem, RefundDetailItem. Request/V2: RetrieveTransactionReportRequest, RetrieveScrollTransactionReportRequest, RetrieveTransactionDetailRequest |

## Silinen (dağıtım DIŞI — git history'de)

Subscription (Model/V2/Subscription + Request/V2/Subscription tamamı), IyziLink/FastLink,
Apm*, Bkm* (Basic dahil), CheckoutForm*, Iyziup*, PayWithIyzico*, UcsInit/InitUcs,
Loyalty* (LoyaltyReward dahil), CardBlacklist*, CardManagementPage*, Threeds* (Basic ve
V2 istekleri dahil), BankTransfer, BasicPayment* ailesi, Approval/Disapproval,
SubMerchantC2C + C2C istekleri, ProductBuyerInfo, "RetrieveBkmRequest .cs" (bozuk adlı),
tüm " 2" iCloud kopyaları, README.md — VE beş proje: CP.VPOS, Iyzipay, Iyzipay.Samples,
Iyzipay.Tests, Iyzipay.Tests.Functional; artı üç ölü BC test projesi.

## Onarım eşlemesi

| Dosya | İşlem |
|-------|-------|
| 3× `Program.cs` | Silinen tiplerin Marten şema kayıtları, endpoint map'leri, MCP kayıtları, seeder'lar temizlenir; Wolverine Shared-event publish kayıtları KALIR |
| 3× `GlobalUsings.cs` | Ölü namespace satırları çıkar; yeni Domains/Provider namespace'leri gerekirse eklenir |
| `Payment.Api.csproj` | CP.VPOS referansı çıkar; SharedKernel referansı BinCard'sız gereksizse çıkar; Newtonsoft eklenir (sürümsüz, CPM) |
| `Merchant.Api.csproj` / `Commission.Api.csproj` | Newtonsoft eklenir (sürümsüz); Commission'ın SharedKernel referansı kullanım kalmadıysa çıkar |
| `Merchant.Api/ReadModels/MerchantCommissionGridReadyHandler` | Ölü Merchant aggregate'ine bakıyorsa silinir |
| `CardVault/SimulatedCardVault` | BinCard/CardInfo bağı vault-içi minimal tiple onarılır |
| `src/others/SharedKernel` | Tüketen kalmadıysa proje de silinir (kararı implement verir — tarama sonucu) |