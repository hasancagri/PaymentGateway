using System.Globalization;
using Iyz = Payment.Api.Utils;
using Payment.Api.Options;
// Domain VO'ları (035) — handler VO'dan wire'a map'ler (anti-corruption sınır).
using DomainBuyer = Payment.Api.Domains.Payments.ValueObjects.Buyer;
using DomainAddress = Payment.Api.Domains.Payments.ValueObjects.Address;
using DomainBasketItem = Payment.Api.Domains.Payments.ValueObjects.BasketItem;

namespace Payment.Api.Domains.Payments.Features.Commands;

/// <summary>
/// 033 US1: kayıtlı kartla NonSecure çekim. Vault token → StoredCard (kiracı + Active kontrolü) →
/// iyzico ödeme (cardToken/cardUserKey; CVC/PAN YOK). Başarıda Payment kaydı + PaymentChargedEvent
/// (iyzico maliyeti); başarısızda Failed kaydı (olay yok). Efektif komisyon HESAPLANMAZ (Commission BC).
/// </summary>
public static class ChargePayment
{
    public record BuyerInput(string Name, string Surname, string Email, string GsmNumber,
        string IdentityNumber, string RegistrationAddress, string City, string Country, string Ip);

    public record BasketItemInput(string Id, string Name, string Category1, decimal Price);

    public record ChargePaymentCommand(
        Guid MerchantId, string VaultToken, decimal Price, decimal PaidPrice, int Installment,
        BuyerInput Buyer, List<BasketItemInput> BasketItems);

    public record ChargePaymentBody(
        string VaultToken, decimal Price, decimal PaidPrice, int Installment,
        BuyerInput Buyer, List<BasketItemInput> BasketItems);

    public class ChargePaymentResponse
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
    public class ChargePaymentCommandHandler
    {
        public async Task<FeatureObjectResultModel<ChargePaymentResponse>> Handle(
            ChargePaymentCommand cmd, IDocumentSession session, Iyz.ProviderOptions providerOptions,
            IyzicoRequestOptions requestOptions, IMessageBus bus, CancellationToken ct)
        {
            // Vault token → StoredCard: kiracı sınırı + Active (Revoked/yabancı reddi — FR-002).
            var card = await session.LoadAsync<StoredCard>(cmd.VaultToken, ct);
            if (card is null || card.MerchantId != cmd.MerchantId)
                return FeatureObjectResultModel<ChargePaymentResponse>.Error(new MessageItem
                { Property = nameof(cmd.VaultToken), Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND });
            if (card.Status != StoredCardStatus.Active)
                return FeatureObjectResultModel<ChargePaymentResponse>.Error(new MessageItem
                { Property = nameof(cmd.VaultToken), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });

            // HTTP Input DTO → domain VO (doğrulama). Geçersizse charge domain-sonucuyla reddedilir (035).
            var buyerResult = DomainBuyer.Create(
                cmd.Buyer.Name, cmd.Buyer.Surname, cmd.Buyer.Email, cmd.Buyer.GsmNumber,
                cmd.Buyer.IdentityNumber, cmd.Buyer.RegistrationAddress, cmd.Buyer.City,
                cmd.Buyer.Country, cmd.Buyer.Ip);
            if (!buyerResult.IsSuccess)
                return FeatureObjectResultModel<ChargePaymentResponse>.Error(buyerResult.Messages);

            var basketItems = new List<DomainBasketItem>();
            foreach (var i in cmd.BasketItems)
            {
                var itemResult = DomainBasketItem.Create(i.Id, i.Name, i.Category1, i.Price);
                if (!itemResult.IsSuccess)
                    return FeatureObjectResultModel<ChargePaymentResponse>.Error(itemResult.Messages);
                basketItems.Add(itemResult.Data!);
            }

            var addressResult = DomainAddress.FromBuyer(buyerResult.Data!);
            if (!addressResult.IsSuccess)
                return FeatureObjectResultModel<ChargePaymentResponse>.Error(addressResult.Messages);

            var request = BuildRequest(cmd, card, buyerResult.Data!, addressResult.Data!, basketItems, requestOptions);

            PaymentResult iyzicoPayment;
            try
            {
                var uri = providerOptions.BaseUrl + requestOptions.PaymentAuthPath;
                var headers = Iyz.ProviderResourceV2.GetHttpHeadersWithRequestBody(request, uri, providerOptions, request.ConversationId);
                iyzicoPayment = await Iyz.RestHttpClientV2.Create().PostAsync<PaymentResult>(uri, headers, request);
            }
            catch
            {
                await StoreFailed(cmd, session);
                return FeatureObjectResultModel<ChargePaymentResponse>.Error(new MessageItem
                { Property = nameof(cmd.VaultToken), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });
            }

            if (iyzicoPayment is null || iyzicoPayment.Status != requestOptions.SuccessStatus ||
                string.IsNullOrWhiteSpace(iyzicoPayment.PaymentId))
            {
                await StoreFailed(cmd, session);
                return FeatureObjectResultModel<ChargePaymentResponse>.Error(new MessageItem
                { Property = nameof(cmd.VaultToken), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });
            }

            var result = Payment.Succeeded(
                cmd.MerchantId, cmd.VaultToken, cmd.Price, cmd.PaidPrice, cmd.Installment,
                iyzicoPayment.PaymentId, iyzicoPayment.IyziCommissionRateAmount ?? string.Empty,
                iyzicoPayment.IyziCommissionFee ?? string.Empty);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<ChargePaymentResponse>.Error(result.Messages);

            var payment = result.Data!;
            session.Store(payment);

            // [Transactional] outbox: yayın yalnız DB commit'te gider (FR-005).
            await bus.PublishAsync(new Shared.IntegrationEvents.PaymentChargedEvent(
                payment.Id, payment.MerchantId, payment.Price, payment.PaidPrice, payment.Installment,
                payment.ProviderCommission, payment.ProviderFee, payment.ProviderPaymentId));

            return FeatureObjectResultModel<ChargePaymentResponse>.Ok(new ChargePaymentResponse
            {
                PaymentId = payment.Id,
                ProviderPaymentId = payment.ProviderPaymentId,
                Status = payment.Status.ToString(),
                Price = payment.Price,
                PaidPrice = payment.PaidPrice,
                Installment = payment.Installment
            });
        }

        private static async Task StoreFailed(ChargePaymentCommand cmd, IDocumentSession session)
        {
            var failed = Payment.Failed(cmd.MerchantId, cmd.VaultToken, cmd.Price, cmd.Installment);
            if (failed.IsSuccess)
            {
                session.Store(failed.Data!);
                await session.SaveChangesAsync();
            }
        }

        // Domain VO → wire DTO (anti-corruption sınır, 035). Sabitler config'ten (requestOptions).
        private static CreatePaymentRequest BuildRequest(
            ChargePaymentCommand cmd, StoredCard card, DomainBuyer buyer, DomainAddress address,
            List<DomainBasketItem> basketItems, IyzicoRequestOptions opt)
        {
            var inv = CultureInfo.InvariantCulture;
            var merchantShort = cmd.MerchantId.ToString("N")[..8];
            return new CreatePaymentRequest
            {
                Locale = opt.Locale,
                ConversationId = opt.ConversationId,
                Price = cmd.Price.ToString(inv),
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
                BasketItems = basketItems.Select(i => new BasketItem
                {
                    Id = i.Id,
                    Name = i.Name,
                    Category1 = i.Category1,
                    ItemType = opt.ItemType,
                    Price = i.Price.ToString(inv)
                }).ToList()
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

public static class ChargePaymentEndpoint
{
    public static RouteGroupBuilder ChargePaymentGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/",
                async (Guid merchantId, [FromBody] ChargePayment.ChargePaymentBody body, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<ChargePayment.ChargePaymentResponse>>(
                        new ChargePayment.ChargePaymentCommand(
                            merchantId, body.VaultToken, body.Price, body.PaidPrice, body.Installment,
                            body.Buyer, body.BasketItems));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("ChargePayment")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.PaymentCharge, AuthorizationPolicies.MerchantScoped)
            .Produces<ChargePayment.ChargePaymentResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        return group;
    }
}