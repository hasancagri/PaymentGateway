using System.Globalization;
using Payment.Api.Domains.StoredCards;
using Payment.Api.Provider;
using Payment.Api.Provider.Payments;

namespace Payment.Api.Domains.Payments.Features.Commands;

/// <summary>
/// 033 US1: kayıtlı kartla NonSecure çekim. Vault token → StoredCard (kiracı + Active kontrolü) →
/// iyzico Payment.Create (cardToken/cardUserKey; CVC/PAN YOK). Başarıda Payment kaydı + PaymentChargedEvent
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

    [Transactional]
    public class ChargePaymentCommandHandler
    {
        public async Task<FeatureObjectResultModel<ChargePaymentResponse>> Handle(
            ChargePaymentCommand cmd, IDocumentSession session, ProviderOptions providerOptions,
            IMessageBus bus, CancellationToken ct)
        {
            // Vault token → StoredCard: kiracı sınırı + Active (Revoked/yabancı reddi — FR-002).
            var card = await session.LoadAsync<StoredCard>(cmd.VaultToken, ct);
            if (card is null || card.MerchantId != cmd.MerchantId)
                return FeatureObjectResultModel<ChargePaymentResponse>.Error(new MessageItem
                { Property = nameof(cmd.VaultToken), Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND });
            if (card.Status != StoredCardStatus.Active)
                return FeatureObjectResultModel<ChargePaymentResponse>.Error(new MessageItem
                { Property = nameof(cmd.VaultToken), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });

            var request = BuildRequest(cmd, card);

            Provider.Payments.Payment iyzicoPayment;
            try
            {
                iyzicoPayment = await Provider.Payments.Payment.Create(request, providerOptions);
            }
            catch
            {
                await StoreFailed(cmd, session);
                return FeatureObjectResultModel<ChargePaymentResponse>.Error(new MessageItem
                { Property = nameof(cmd.VaultToken), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });
            }

            if (iyzicoPayment is null || iyzicoPayment.Status != "success" ||
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
                payment.IyzicoCommission, payment.IyzicoFee, payment.ProviderPaymentId));

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

        private static CreatePaymentRequest BuildRequest(ChargePaymentCommand cmd, StoredCard card)
        {
            var inv = CultureInfo.InvariantCulture;
            var b = cmd.Buyer;
            return new CreatePaymentRequest
            {
                Locale = "tr",
                ConversationId = "pay-" + cmd.MerchantId.ToString("N")[..8],
                Price = cmd.Price.ToString(inv),
                PaidPrice = cmd.PaidPrice.ToString(inv),
                Installment = cmd.Installment,
                PaymentChannel = "WEB",
                PaymentGroup = "PRODUCT",
                Currency = "TRY",
                BasketId = "B-" + cmd.MerchantId.ToString("N")[..8],
                PaymentCard = new PaymentCard
                {
                    CardToken = card.CardToken,
                    CardUserKey = card.CardUserKey
                },
                Buyer = new Buyer
                {
                    Id = "BY-" + cmd.MerchantId.ToString("N")[..8],
                    Name = b.Name,
                    Surname = b.Surname,
                    Email = b.Email,
                    GsmNumber = b.GsmNumber,
                    IdentityNumber = b.IdentityNumber,
                    RegistrationAddress = b.RegistrationAddress,
                    City = b.City,
                    Country = b.Country,
                    Ip = b.Ip
                },
                ShippingAddress = BuildAddress(b),
                BillingAddress = BuildAddress(b),
                BasketItems = cmd.BasketItems.Select(i => new BasketItem
                {
                    Id = i.Id,
                    Name = i.Name,
                    Category1 = i.Category1,
                    ItemType = "PHYSICAL",
                    Price = i.Price.ToString(inv)
                }).ToList()
            };
        }

        private static Address BuildAddress(BuyerInput b) => new()
        {
            ContactName = $"{b.Name} {b.Surname}",
            City = b.City,
            Country = b.Country,
            Description = b.RegistrationAddress
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
