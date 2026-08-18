using System.Globalization;
using Iyz = Payment.Api.Utils;
using Payment.Api.Options;
// Domain VO'ları (035) — handler VO'dan wire'a map'ler (anti-corruption sınır).
using DomainBuyer = Payment.Api.Domains.Payments.ValueObjects.Buyer;
using DomainAddress = Payment.Api.Domains.Payments.ValueObjects.Address;
using DomainBasketItem = Payment.Api.Domains.Payments.ValueObjects.BasketItem;

namespace Payment.Api.Domains.Payments.Features.Commands;

/// <summary>
/// 039: yapısal İDEMPOTENT çekim (ECom Order.Api server-to-server REST, X-Api-Key). 033'ün REST charge
/// slice'ı buraya evrildi (başka çağıranı yoktu). İstek `correlationKey` taşır; sepet İSTEKLE GELMEZ
/// (para-manipülasyon yüzeyi kapalı) — tek sentetik kalem sentezlenir. İdempotency: aynı key → var
/// olan ödeme döner, iyzico'ya GİDİLMEZ (çift çekim yok). Kayıp-yanıt koruması: iyzico çağrısından
/// ÖNCE Charging marker persist edilir (FR-012); retry marker'ı bulur. Statü kapısı: Active değilse
/// fail-closed. Yanıt statüsü ECom sözleşmesi gereği LOWERCASE (success/failed/pending).
/// </summary>
public static class ChargePayment
{
    public record BuyerInput(string Name, string Surname, string Email, string GsmNumber,
        string IdentityNumber, string RegistrationAddress, string City, string Country, string Ip);

    // 039: istek gövdesi — correlationKey EKLENDİ, basketItems KALDIRILDI (sepet gateway'de sentezlenir).
    public record ChargePaymentBody(
        string CorrelationKey, string VaultToken, decimal Price, decimal PaidPrice, int Installment,
        BuyerInput Buyer);

    public record ChargePaymentCommand(
        Guid MerchantId, string CorrelationKey, string VaultToken, decimal Price, decimal PaidPrice,
        int Installment, BuyerInput Buyer);

    // 039: ECom PaymentReply eşleniği — Status LOWERCASE wire değeri (enum adı DEĞİL). CorrelationKey echo.
    public class ChargePaymentResponse
    {
        public Guid PaymentId { get; set; }
        public string Status { get; set; } = string.Empty; // success / failed / pending
        public decimal Price { get; set; }
        public decimal PaidPrice { get; set; }
        public string CorrelationKey { get; set; } = string.Empty;
    }

    // Statü kapısı domain sözleşme değeri (merchant.lifecycle string'i — iyzico sabiti değil).
    private const string ActiveStatus = "Active";

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
    public class ChargePaymentCommandHandler
    {
        public async Task<FeatureObjectResultModel<ChargePaymentResponse>> Handle(
            ChargePaymentCommand cmd, IDocumentSession session, Iyz.ProviderOptions providerOptions,
            IyzicoRequestOptions requestOptions, IMessageBus bus, CancellationToken ct)
        {
            // Statü kapısı (FR-009): referans yok veya Active değil → fail-closed (sağlayıcıya gidilmez).
            var merchantStatus = await session.LoadAsync<MerchantStatusReference>(cmd.MerchantId, ct);
            if (merchantStatus is null ||
                !string.Equals(merchantStatus.Status, ActiveStatus, StringComparison.OrdinalIgnoreCase))
                return FeatureObjectResultModel<ChargePaymentResponse>.Error(new MessageItem
                { Property = nameof(cmd.MerchantId), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });

            // İdempotency (FR-002): aynı correlationKey'e bağlı ödeme varsa iyzico'ya GİTME, var olanı dön.
            var existing = await session.Query<Payment>()
                .Where(x => x.MerchantId == cmd.MerchantId && x.CorrelationKey == cmd.CorrelationKey)
                .FirstOrDefaultAsync(ct);
            if (existing is not null)
                return FeatureObjectResultModel<ChargePaymentResponse>.Ok(MapResponse(existing));

            // Vault token → StoredCard: kiracı sınırı + Active (Revoked/yabancı reddi).
            var card = await session.LoadAsync<StoredCard>(cmd.VaultToken, ct);
            if (card is null || card.MerchantId != cmd.MerchantId)
                return FeatureObjectResultModel<ChargePaymentResponse>.Error(new MessageItem
                { Property = nameof(cmd.VaultToken), Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND });
            if (card.Status != StoredCardStatus.Active)
                return FeatureObjectResultModel<ChargePaymentResponse>.Error(new MessageItem
                { Property = nameof(cmd.VaultToken), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });

            // HTTP Input DTO → domain VO (doğrulama) — buyer GERÇEK müşteri verisi (ECom'den verbatim).
            var buyerResult = DomainBuyer.Create(
                cmd.Buyer.Name, cmd.Buyer.Surname, cmd.Buyer.Email, cmd.Buyer.GsmNumber,
                cmd.Buyer.IdentityNumber, cmd.Buyer.RegistrationAddress, cmd.Buyer.City,
                cmd.Buyer.Country, cmd.Buyer.Ip);
            if (!buyerResult.IsSuccess)
                return FeatureObjectResultModel<ChargePaymentResponse>.Error(buyerResult.Messages);

            // Sentetik tek sepet kalemi (config) — sepet ECom'da yaşar, gateway'e taşınmaz (FR-008).
            var itemResult = DomainBasketItem.Create(
                requestOptions.BasketItemId, requestOptions.BasketItemName,
                requestOptions.BasketItemCategory, cmd.Price);
            if (!itemResult.IsSuccess)
                return FeatureObjectResultModel<ChargePaymentResponse>.Error(itemResult.Messages);

            var addressResult = DomainAddress.FromBuyer(buyerResult.Data!);
            if (!addressResult.IsSuccess)
                return FeatureObjectResultModel<ChargePaymentResponse>.Error(addressResult.Messages);

            // FR-012: iyzico ÖNCESİ Charging marker persist (kayıp-yanıtta retry bunu bulur, tekrar çekmez).
            var beginResult = Payment.Begin(
                cmd.MerchantId, cmd.VaultToken, cmd.CorrelationKey, cmd.Price, cmd.PaidPrice, cmd.Installment);
            if (!beginResult.IsSuccess)
                return FeatureObjectResultModel<ChargePaymentResponse>.Error(beginResult.Messages);

            var payment = beginResult.Data!;
            session.Store(payment);
            try
            {
                await session.SaveChangesAsync(ct); // marker'ı sağlayıcı çağrısından ÖNCE commit et
            }
            catch
            {
                // Yarış (FR-003): eşzamanlı istek aynı key'i yazdı (unique index) → var olanı dön, çekme.
                session.Eject(payment); // [Transactional] auto-save çakışan insert'i tekrar denemesin
                var raced = await session.Query<Payment>()
                    .Where(x => x.MerchantId == cmd.MerchantId && x.CorrelationKey == cmd.CorrelationKey)
                    .FirstOrDefaultAsync(ct);
                if (raced is not null)
                    return FeatureObjectResultModel<ChargePaymentResponse>.Ok(MapResponse(raced));
                throw;
            }

            var request = BuildRequest(cmd, card, buyerResult.Data!, addressResult.Data!, itemResult.Data!, requestOptions);

            PaymentResult iyzicoPayment;
            try
            {
                var uri = providerOptions.BaseUrl + requestOptions.PaymentAuthPath;
                var headers = Iyz.ProviderResourceV2.GetHttpHeadersWithRequestBody(request, uri, providerOptions, request.ConversationId);
                iyzicoPayment = await Iyz.RestHttpClientV2.Create().PostAsync<PaymentResult>(uri, headers, request);
            }
            catch
            {
                payment.Fail();
                session.Store(payment);
                return FeatureObjectResultModel<ChargePaymentResponse>.Ok(MapResponse(payment));
            }

            if (iyzicoPayment is null || iyzicoPayment.Status != requestOptions.SuccessStatus ||
                string.IsNullOrWhiteSpace(iyzicoPayment.PaymentId))
            {
                payment.Fail();
                session.Store(payment);
                return FeatureObjectResultModel<ChargePaymentResponse>.Ok(MapResponse(payment));
            }

            var succeedResult = payment.Succeed(
                iyzicoPayment.PaymentId, iyzicoPayment.IyziCommissionRateAmount ?? string.Empty,
                iyzicoPayment.IyziCommissionFee ?? string.Empty);
            if (!succeedResult.IsSuccess)
                return FeatureObjectResultModel<ChargePaymentResponse>.Error(succeedResult.Messages);
            session.Store(payment);

            // [Transactional] outbox: yayın yalnız DB commit'te gider.
            await bus.PublishAsync(new Shared.IntegrationEvents.PaymentChargedEvent(
                payment.Id, payment.MerchantId, payment.Price, payment.PaidPrice, payment.Installment,
                payment.ProviderCommission, payment.ProviderFee, payment.ProviderPaymentId));

            return FeatureObjectResultModel<ChargePaymentResponse>.Ok(MapResponse(payment));
        }

        // Domain durumu → ECom wire yanıtı. Status LOWERCASE (ECom Map success/failed/pending bekler).
        private static ChargePaymentResponse MapResponse(Payment p) => new()
        {
            PaymentId = p.Id,
            Status = p.Status switch
            {
                PaymentStatus.Success => "success",
                PaymentStatus.Failed => "failed",
                _ => "pending" // Charging → belirsiz, ECom reconcile eder
            },
            Price = p.Price,
            PaidPrice = p.PaidPrice,
            CorrelationKey = p.CorrelationKey ?? string.Empty
        };

        // Domain VO → wire DTO (anti-corruption sınır, 035). Sabitler config'ten (requestOptions).
        private static CreatePaymentRequest BuildRequest(
            ChargePaymentCommand cmd, StoredCard card, DomainBuyer buyer, DomainAddress address,
            DomainBasketItem basketItem, IyzicoRequestOptions opt)
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

public static class ChargePaymentEndpoint
{
    public static RouteGroupBuilder ChargePaymentGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/",
                async (Guid merchantId, [FromBody] ChargePayment.ChargePaymentBody body, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<ChargePayment.ChargePaymentResponse>>(
                        new ChargePayment.ChargePaymentCommand(
                            merchantId, body.CorrelationKey, body.VaultToken, body.Price, body.PaidPrice,
                            body.Installment, body.Buyer));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("ChargePayment")
            .MapToApiVersion(1, 0)
            // 039: X-Api-Key şeması + merchant_id claim == route {merchantId} (JWT scope DEĞİL).
            .RequireAuthorization(AuthorizationPolicies.MerchantApiKey)
            .Produces<ChargePayment.ChargePaymentResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        return group;
    }
}
