using CP.VPOS;
using CP.VPOS.Enums;
using CP.VPOS.Models;
using Payment.Api.Domains.BinCards.Features.Queries;
using Shared;

namespace Payment.Api.Domains.Payments.Features.Commands;

public static class ProcessPayment
{
    public record ProcessPaymentCommand(
        string OrderNumber,
        decimal Amount,
        int InstallmentCount,
        string CardNameSurname,
        string CardNumber,
        short CardExpiryMonth,
        ushort CardExpiryYear,
        string CardCvv,
        bool Use3D,
        string? Callback3DUrl,
        string CustomerIpAddress,
        CustomerDto Customer);

    /// <summary>Fatura ve teslimat için ortak müşteri bilgisi (dropship'te ikisi aynıdır).</summary>
    public record CustomerDto(
        string Name,
        string Surname,
        string EmailAddress,
        string PhoneNumber,
        string TaxNumber,
        string? CityName,
        string? TownName,
        string? AddressDesc,
        string? PostCode);

    public class ProcessPaymentResponse
    {
        public Guid PaymentId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? BankCode { get; set; }
        public string? TransactionId { get; set; }

        /// <summary>3D akışında bankanın yönlendirme içeriği (URL veya HTML).</summary>
        public string? Redirect3DContent { get; set; }

        public List<string> TriedBankCodes { get; set; } = new();
    }

    [Transactional]
    public class ProcessPaymentCommandHandler
    {
        public async Task<FeatureObjectResultModel<ProcessPaymentResponse>> Handle(
            ProcessPaymentCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var paymentResult = Payment.Create(cmd.OrderNumber, cmd.Amount, cmd.InstallmentCount);
            if (!paymentResult.IsSuccess)
                return FeatureObjectResultModel<ProcessPaymentResponse>.Error(paymentResult.Messages);

            var payment = paymentResult.Data!;

            var accounts = await session.Query<PosAccount>()
                .Where(a => a.IsActive && !a.IsDeleted)
                .ToListAsync(ct);

            // BIN artık donmuş kütüphaneden değil katalogtan çözülür; bulunamazsa null → sahte-default üretme.
            var card = await ResolveBinCard.Resolve(session, cmd.CardNumber, ct);
            if (card is null)
                return FeatureObjectResultModel<ProcessPaymentResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.CardNumber),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var routeResult = BankRouter.Route(cmd.Amount, cmd.InstallmentCount, card, accounts.ToList());
            if (!routeResult.IsSuccess)
                return FeatureObjectResultModel<ProcessPaymentResponse>.Error(routeResult.Messages);

            var candidates = routeResult.Data!;

            // 3D akışında kullanıcı tek bankanın 3D sayfasına yönlendirilebilir; failover yalnız non-3D'de.
            if (cmd.Use3D)
                candidates = candidates.Take(1).ToList();

            var response = new ProcessPaymentResponse { PaymentId = payment.Id };

            foreach (var candidate in candidates)
            {
                var saleResponse = VPOSClient.Sale(BuildSaleRequest(cmd), BuildAuth(candidate.Account));
                response.TriedBankCodes.Add(candidate.Account.BankCode);

                if (cmd.Use3D &&
                    saleResponse.statu is SaleResponseStatu.RedirectURL or SaleResponseStatu.RedirectHTML)
                {
                    payment.MarkAwaiting3D(candidate.Account.BankCode);
                    payment.RecordAttempt(candidate.Account.BankCode, true, "3D yönlendirme");
                    response.Redirect3DContent = saleResponse.message;
                    break;
                }

                if (saleResponse.statu == SaleResponseStatu.Success)
                {
                    payment.RecordAttempt(candidate.Account.BankCode, true, saleResponse.message);
                    payment.MarkCompleted(candidate.Account.BankCode, saleResponse.transactionId);
                    response.TransactionId = saleResponse.transactionId;

                    await bus.PublishAsync(new Shared.IntegrationEvents.PaymentCompletedEvent(
                        payment.Id, payment.OrderNumber, payment.Amount, candidate.Account.BankCode));
                    break;
                }

                payment.RecordAttempt(candidate.Account.BankCode, false, saleResponse.message);
            }

            if (payment.Status == PaymentStatus.Pending)
            {
                var reason = payment.Attempts.LastOrDefault()?.Message;
                payment.MarkFailed(reason);

                await bus.PublishAsync(new Shared.IntegrationEvents.PaymentFailedEvent(
                    payment.Id, payment.OrderNumber, payment.Amount, reason));
            }

            session.Store(payment);

            response.Status = payment.Status.ToString();
            response.BankCode = payment.BankCode;
            return FeatureObjectResultModel<ProcessPaymentResponse>.Ok(response);
        }

        internal static VirtualPOSAuth BuildAuth(PosAccount account)
        {
            return new VirtualPOSAuth
            {
                bankCode = account.BankCode,
                merchantID = account.MerchantId,
                merchantUser = account.MerchantUser,
                merchantPassword = account.MerchantPassword,
                merchantStorekey = account.MerchantStorekey,
                testPlatform = account.TestPlatform
            };
        }

        private static SaleRequest BuildSaleRequest(ProcessPaymentCommand cmd)
        {
            var customer = new CustomerInfo
            {
                name = cmd.Customer.Name,
                surname = cmd.Customer.Surname,
                emailAddress = cmd.Customer.EmailAddress,
                phoneNumber = cmd.Customer.PhoneNumber,
                taxNumber = cmd.Customer.TaxNumber,
                cityName = cmd.Customer.CityName,
                townName = cmd.Customer.TownName,
                addressDesc = cmd.Customer.AddressDesc,
                postCode = cmd.Customer.PostCode
            };

            return new SaleRequest
            {
                orderNumber = cmd.OrderNumber,
                customerIPAddress = cmd.CustomerIpAddress,
                saleInfo = new SaleInfo
                {
                    cardNameSurname = cmd.CardNameSurname,
                    cardNumber = cmd.CardNumber,
                    cardExpiryDateMonth = cmd.CardExpiryMonth,
                    cardExpiryDateYear = cmd.CardExpiryYear,
                    cardCVV = cmd.CardCvv,
                    amount = cmd.Amount,
                    currency = Currency.TRY,
                    installment = (sbyte)cmd.InstallmentCount
                },
                invoiceInfo = customer,
                shippingInfo = customer,
                payment3D = new Payment3D
                {
                    confirm = cmd.Use3D,
                    returnURL = cmd.Callback3DUrl
                }
            };
        }
    }
}

public static class ProcessPaymentCommandEndpoint
{
    public static RouteGroupBuilder ProcessPaymentGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/",
                async ([FromBody] ProcessPayment.ProcessPaymentCommand cmd, HttpContext httpContext, IMessageBus bus) =>
                {
                    var command = string.IsNullOrWhiteSpace(cmd.CustomerIpAddress)
                        ? cmd with { CustomerIpAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0" }
                        : cmd;

                    var result =
                        await bus.InvokeAsync<FeatureObjectResultModel<ProcessPayment.ProcessPaymentResponse>>(command);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("ProcessPayment")
            .MapToApiVersion(1, 0)
            .Produces<ProcessPayment.ProcessPaymentResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}