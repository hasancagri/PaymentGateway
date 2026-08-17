using System.Globalization;
using Iyz = Payment.Api.Utils;
using Payment.Api.Options;
using Payment.Api.Domains.MerchantStatus;
// Domain VO'ları (035) — handler VO'dan wire'a map'ler (anti-corruption sınır).
using DomainBuyer = Payment.Api.Domains.Payments.ValueObjects.Buyer;
using DomainAddress = Payment.Api.Domains.Payments.ValueObjects.Address;
using DomainBasketItem = Payment.Api.Domains.Payments.ValueObjects.BasketItem;

namespace Payment.Api.Domains.Payments.Features.Agents;

/// <summary>
/// 038 US2: Agent yüzeyi — kayıtlı karttan GERÇEK çekim. MCP tool'u (charge_saved_card) YALNIZ bu
/// slice'ı çağırır (015/016). /mcp makine token'ı statü taşımadığından merchant statü kapısı BURADA:
/// MerchantStatusReference yok veya Active değil → fail-closed RET (sağlayıcıya gidilmez, anayasa
/// İlke V "charge yalnız Active"). Buyer GERÇEK müşteri bilgisidir (ECommerce toplar, A2A verbatim
/// taşır); sepet İSTEKLE GELMEZ — iyzico'nun zorunlu basketItems alanı tek sentetik kalemle
/// (config, price = Amount) sentezlenir (kullanıcı kararı 2026-08-16). Wire tipleri slice'a nested
/// (037); Commands/ChargePayment'tan bilinçli kopya (kod tekrarı OK).
/// </summary>
public static class ChargeSavedCardForAgent
{
    // Statü kapısı domain sözleşme değeri (merchant.lifecycle event status string'i — iyzico sabiti değil).
    private const string ActiveStatus = "Active";

    public record BuyerInput(string Name, string Surname, string Email, string GsmNumber,
        string IdentityNumber, string RegistrationAddress, string City, string Country, string Ip);

    public record ChargeSavedCardForAgentCommand(
        Guid MerchantId, string VaultToken, decimal Amount, decimal PaidPrice, int Installment,
        BuyerInput Buyer);

    public class ChargeResultView
    {
        public Guid PaymentId { get; set; }
        public string ProviderPaymentId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal PaidPrice { get; set; }
        public int Installment { get; set; }
    }

    // --- iyzico wire tipleri (bu slice'a ait; camelCase JSON, base tip yok) ---

    public class CreatePaymentRequest
    {
        public string Locale { get; set; } = string.Empty;
        public string ConversationId { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string PaidPrice { get; set; } = string.Empty;
        public int Installment { get; set; }
        public string PaymentChannel { get; set; } = string.Empty;
        public string PaymentGroup { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public string BasketId { get; set; } = string.Empty;
        public PaymentCard PaymentCard { get; set; } = new();
        public Buyer Buyer { get; set; } = new();
        public Address ShippingAddress { get; set; } = new();
        public Address BillingAddress { get; set; } = new();
        public List<BasketItem> BasketItems { get; set; } = new();
    }

    public class PaymentCard
    {
        public string CardToken { get; set; } = string.Empty;
        public string CardUserKey { get; set; } = string.Empty;
    }

    public class Buyer
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Surname { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string GsmNumber { get; set; } = string.Empty;
        public string IdentityNumber { get; set; } = string.Empty;
        public string RegistrationAddress { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
    }

    public class Address
    {
        public string ContactName { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        // iyzico wire alanı "address"tir; "description" gönderilirse 5040 "Shipping address zorunludur" döner.
        [Newtonsoft.Json.JsonProperty("address")]
        public string Description { get; set; } = string.Empty;
    }

    public class BasketItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category1 { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
    }

    /// <summary>iyzico ödeme yanıtı (wire) — Status/Error alanları Iyz.ProviderResourceV2'den.</summary>
    public class PaymentResult : Iyz.ProviderResourceV2
    {
        public string PaymentId { get; set; } = string.Empty;
        public string IyziCommissionRateAmount { get; set; } = string.Empty;
        public string IyziCommissionFee { get; set; } = string.Empty;
    }

    [Transactional]
    public class ChargeSavedCardForAgentCommandHandler
    {
        public async Task<FeatureObjectResultModel<ChargeResultView>> Handle(
            ChargeSavedCardForAgentCommand cmd, IDocumentSession session, Iyz.ProviderOptions providerOptions,
            IyzicoRequestOptions requestOptions, IMessageBus bus, CancellationToken ct)
        {
            // 038 statü kapısı: referans yok veya Active değil → fail-closed RET (sağlayıcıya gidilmez).
            var merchantStatus = await session.LoadAsync<MerchantStatusReference>(cmd.MerchantId, ct);
            if (merchantStatus is null ||
                !string.Equals(merchantStatus.Status, ActiveStatus, StringComparison.OrdinalIgnoreCase))
                return FeatureObjectResultModel<ChargeResultView>.Error(new MessageItem
                { Property = nameof(cmd.MerchantId), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });

            // Vault token → StoredCard: kiracı sınırı + Active (Revoked/yabancı reddi).
            var card = await session.LoadAsync<StoredCard>(cmd.VaultToken, ct);
            if (card is null || card.MerchantId != cmd.MerchantId)
                return FeatureObjectResultModel<ChargeResultView>.Error(new MessageItem
                { Property = nameof(cmd.VaultToken), Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND });
            if (card.Status != StoredCardStatus.Active)
                return FeatureObjectResultModel<ChargeResultView>.Error(new MessageItem
                { Property = nameof(cmd.VaultToken), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });

            // Agent Input DTO → domain VO (doğrulama) — buyer GERÇEK müşteri verisi (ECommerce'den).
            var buyerResult = DomainBuyer.Create(
                cmd.Buyer.Name, cmd.Buyer.Surname, cmd.Buyer.Email, cmd.Buyer.GsmNumber,
                cmd.Buyer.IdentityNumber, cmd.Buyer.RegistrationAddress, cmd.Buyer.City,
                cmd.Buyer.Country, cmd.Buyer.Ip);
            if (!buyerResult.IsSuccess)
                return FeatureObjectResultModel<ChargeResultView>.Error(buyerResult.Messages);

            // Sentetik tek sepet kalemi (config) — sepet ECommerce'de yaşar, gateway'e taşınmaz.
            var itemResult = DomainBasketItem.Create(
                requestOptions.BasketItemId, requestOptions.BasketItemName,
                requestOptions.BasketItemCategory, cmd.Amount);
            if (!itemResult.IsSuccess)
                return FeatureObjectResultModel<ChargeResultView>.Error(itemResult.Messages);

            var addressResult = DomainAddress.FromBuyer(buyerResult.Data!);
            if (!addressResult.IsSuccess)
                return FeatureObjectResultModel<ChargeResultView>.Error(addressResult.Messages);

            var request = BuildRequest(cmd, card, buyerResult.Data!, addressResult.Data!, itemResult.Data!, requestOptions);

            PaymentResult iyzicoPayment;
            try
            {
                var uri = providerOptions.BaseUrl + requestOptions.PaymentAuthPath;
                var headers = Iyz.ProviderResourceV2.GetHttpHeadersWithRequestBody(request, uri, providerOptions, request.ConversationId);
                iyzicoPayment = await Iyz.RestHttpClientV2.Create().PostAsync<PaymentResult>(uri, headers, request);
            }
            catch (Exception)
            {
                await StoreFailed(cmd, session);
                return FeatureObjectResultModel<ChargeResultView>.Error(new MessageItem
                { Property = nameof(cmd.VaultToken), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });
            }

            if (iyzicoPayment is null || iyzicoPayment.Status != requestOptions.SuccessStatus ||
                string.IsNullOrWhiteSpace(iyzicoPayment.PaymentId))
            {
                await StoreFailed(cmd, session);
                return FeatureObjectResultModel<ChargeResultView>.Error(new MessageItem
                { Property = nameof(cmd.VaultToken), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });
            }

            var result = Payment.Succeeded(
                cmd.MerchantId, cmd.VaultToken, cmd.Amount, cmd.PaidPrice, cmd.Installment,
                iyzicoPayment.PaymentId, iyzicoPayment.IyziCommissionRateAmount ?? string.Empty,
                iyzicoPayment.IyziCommissionFee ?? string.Empty);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<ChargeResultView>.Error(result.Messages);

            var payment = result.Data!;
            session.Store(payment);

            // [Transactional] outbox: yayın yalnız DB commit'te gider.
            await bus.PublishAsync(new Shared.IntegrationEvents.PaymentChargedEvent(
                payment.Id, payment.MerchantId, payment.Price, payment.PaidPrice, payment.Installment,
                payment.ProviderCommission, payment.ProviderFee, payment.ProviderPaymentId));

            return FeatureObjectResultModel<ChargeResultView>.Ok(new ChargeResultView
            {
                PaymentId = payment.Id,
                ProviderPaymentId = payment.ProviderPaymentId,
                Status = payment.Status.ToString(),
                Price = payment.Price,
                PaidPrice = payment.PaidPrice,
                Installment = payment.Installment
            });
        }

        private static async Task StoreFailed(ChargeSavedCardForAgentCommand cmd, IDocumentSession session)
        {
            var failed = Payment.Failed(cmd.MerchantId, cmd.VaultToken, cmd.Amount, cmd.Installment);
            if (failed.IsSuccess)
            {
                session.Store(failed.Data!);
                await session.SaveChangesAsync();
            }
        }

        // Domain VO → wire DTO (anti-corruption sınır, 035). Sabitler config'ten (requestOptions).
        private static CreatePaymentRequest BuildRequest(
            ChargeSavedCardForAgentCommand cmd, StoredCard card, DomainBuyer buyer, DomainAddress address,
            DomainBasketItem basketItem, IyzicoRequestOptions opt)
        {
            var inv = CultureInfo.InvariantCulture;
            var merchantShort = cmd.MerchantId.ToString("N")[..8];
            return new CreatePaymentRequest
            {
                Locale = opt.Locale,
                ConversationId = opt.ConversationId,
                Price = cmd.Amount.ToString(inv),
                PaidPrice = cmd.PaidPrice.ToString(inv),
                Installment = cmd.Installment,
                PaymentChannel = opt.PaymentChannel,
                PaymentGroup = opt.PaymentGroup,
                Currency = opt.Currency,
                BasketId = opt.BasketIdPrefix + merchantShort,
                PaymentCard = new PaymentCard
                {
                    CardToken = card.CardToken,
                    CardUserKey = card.CardUserKey
                },
                Buyer = new Buyer
                {
                    Id = opt.BuyerIdPrefix + merchantShort,
                    Name = buyer.Name,
                    Surname = buyer.Surname,
                    Email = buyer.Email,
                    GsmNumber = buyer.GsmNumber,
                    IdentityNumber = buyer.IdentityNumber,
                    RegistrationAddress = buyer.RegistrationAddress,
                    City = buyer.City,
                    Country = buyer.Country,
                    Ip = buyer.Ip
                },
                ShippingAddress = ToWireAddress(address),
                BillingAddress = ToWireAddress(address),
                BasketItems =
                [
                    new BasketItem
                    {
                        Id = basketItem.Id,
                        Name = basketItem.Name,
                        Category1 = basketItem.Category1,
                        ItemType = opt.ItemType,
                        Price = basketItem.Price.ToString(inv)
                    }
                ]
            };
        }

        private static Address ToWireAddress(DomainAddress a) => new()
        {
            ContactName = a.ContactName,
            City = a.City,
            Country = a.Country,
            Description = a.Description
        };
    }
}