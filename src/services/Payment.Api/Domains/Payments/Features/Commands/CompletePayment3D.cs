using CP.VPOS;
using CP.VPOS.Enums;
using CP.VPOS.Models;
using Shared;

namespace Payment.Api.Domains.Payments.Features.Commands;

public static class CompletePayment3D
{
    public record CompletePayment3DCommand(Guid PaymentId, Dictionary<string, object> ResponseArray);

    public class CompletePayment3DResponse
    {
        public Guid PaymentId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? TransactionId { get; set; }
        public string? Message { get; set; }
    }

    [Transactional]
    public class CompletePayment3DCommandHandler
    {
        public async Task<FeatureObjectResultModel<CompletePayment3DResponse>> Handle(
            CompletePayment3DCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var payment = await session.LoadAsync<Payment>(cmd.PaymentId, ct);
            if (payment is null || payment.IsDeleted)
                return FeatureObjectResultModel<CompletePayment3DResponse>.NotFound();

            if (payment.Status != PaymentStatus.Awaiting3D || payment.BankCode is null)
            {
                return FeatureObjectResultModel<CompletePayment3DResponse>.Error(new MessageItem
                {
                    Property = nameof(payment.Status),
                    Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR
                });
            }

            var account = await session.Query<PosAccount>()
                .Where(a => a.BankCode == payment.BankCode && a.IsActive && !a.IsDeleted)
                .FirstOrDefaultAsync(ct);
            if (account is null)
                return FeatureObjectResultModel<CompletePayment3DResponse>.NotFound();

            var saleResponse = VPOSClient.Sale3DResponse(new Sale3DResponseRequest
            {
                responseArray = cmd.ResponseArray,
                // Yapı Kredi / Vakıf Katılım bu alanları zorunlu tutar; diğerleri yok sayar.
                currency = Currency.TRY,
                amount = payment.Amount,
                installment = (sbyte)payment.InstallmentCount
            }, ProcessPayment.ProcessPaymentCommandHandler.BuildAuth(account));

            if (saleResponse.statu == SaleResponseStatu.Success)
            {
                payment.RecordAttempt(account.BankCode, true, saleResponse.message);
                payment.MarkCompleted(account.BankCode, saleResponse.transactionId);

                await bus.PublishAsync(new Shared.IntegrationEvents.PaymentCompletedEvent(
                    payment.Id, payment.OrderNumber, payment.Amount, account.BankCode));
            }
            else
            {
                payment.RecordAttempt(account.BankCode, false, saleResponse.message);
                payment.MarkFailed(saleResponse.message);

                await bus.PublishAsync(new Shared.IntegrationEvents.PaymentFailedEvent(
                    payment.Id, payment.OrderNumber, payment.Amount, saleResponse.message));
            }

            session.Store(payment);

            return FeatureObjectResultModel<CompletePayment3DResponse>.Ok(new CompletePayment3DResponse
            {
                PaymentId = payment.Id,
                Status = payment.Status.ToString(),
                TransactionId = payment.TransactionId,
                Message = saleResponse.message
            });
        }
    }
}

public static class CompletePayment3DCommandEndpoint
{
    public static RouteGroupBuilder CompletePayment3DGroupItemEndpoint(this RouteGroupBuilder group)
    {
        // Bankalar 3D sonucunu form-post ile döner; form, handler'a sözlük olarak taşınır.
        group.MapPost("/{paymentId:guid}/3d-callback",
                async (Guid paymentId, HttpContext httpContext, IMessageBus bus) =>
                {
                    var responseArray = httpContext.Request.HasFormContentType
                        ? httpContext.Request.Form.ToDictionary(f => f.Key, f => (object)f.Value.ToString())
                        : new Dictionary<string, object>();

                    var result = await bus.InvokeAsync<FeatureObjectResultModel<CompletePayment3D.CompletePayment3DResponse>>(
                        new CompletePayment3D.CompletePayment3DCommand(paymentId, responseArray));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("CompletePayment3D")
            .MapToApiVersion(1, 0)
            .Produces<CompletePayment3D.CompletePayment3DResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}