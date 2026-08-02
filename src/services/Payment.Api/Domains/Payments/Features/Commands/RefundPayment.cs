using CP.VPOS;
using CP.VPOS.Enums;
using CP.VPOS.Models;

namespace Payment.Api.Domains.Payments.Features.Commands;

public static class RefundPayment
{
    public record RefundPaymentCommand(Guid PaymentId, decimal RefundAmount, string? CustomerIpAddress);

    public class RefundPaymentResponse
    {
        public Guid PaymentId { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal RefundedAmount { get; set; }
        public string? Message { get; set; }
    }

    [Transactional]
    public class RefundPaymentCommandHandler
    {
        public async Task<FeatureObjectResultModel<RefundPaymentResponse>> Handle(
            RefundPaymentCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var payment = await session.LoadAsync<Payment>(cmd.PaymentId, ct);
            if (payment is null || payment.IsDeleted)
                return FeatureObjectResultModel<RefundPaymentResponse>.NotFound();

            if (payment.BankCode is null || payment.TransactionId is null)
            {
                return FeatureObjectResultModel<RefundPaymentResponse>.Error(new MessageItem
                {
                    Property = nameof(payment.TransactionId),
                    Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR
                });
            }

            // Invariant'ı (toplam iade <= tutar) banka çağrısından ÖNCE aggregate doğrular.
            var refundResult = payment.Refund(cmd.RefundAmount);
            if (!refundResult.IsSuccess)
                return FeatureObjectResultModel<RefundPaymentResponse>.Error(refundResult.Messages);

            var account = await session.Query<PosAccount>()
                .Where(a => a.BankCode == payment.BankCode && a.IsActive && !a.IsDeleted)
                .FirstOrDefaultAsync(ct);
            if (account is null)
                return FeatureObjectResultModel<RefundPaymentResponse>.NotFound();

            var refundResponse = VPOSClient.Refund(new RefundRequest
            {
                customerIPAddress = cmd.CustomerIpAddress ?? "0.0.0.0",
                orderNumber = payment.OrderNumber,
                transactionId = payment.TransactionId,
                refundAmount = cmd.RefundAmount,
                currency = Currency.TRY
            }, ProcessPayment.ProcessPaymentCommandHandler.BuildAuth(account));

            if (refundResponse.statu != ResponseStatu.Success)
            {
                // Banka reddetti: aggregate değişikliği store edilmez, transaction geri döner.
                return FeatureObjectResultModel<RefundPaymentResponse>.Error(new MessageItem
                {
                    Property = nameof(payment.RefundedAmount),
                    Code = refundResponse.message
                });
            }

            session.Store(payment);

            return FeatureObjectResultModel<RefundPaymentResponse>.Ok(new RefundPaymentResponse
            {
                PaymentId = payment.Id,
                Status = payment.Status.ToString(),
                RefundedAmount = payment.RefundedAmount,
                Message = refundResponse.message
            });
        }
    }
}

public static class RefundPaymentCommandEndpoint
{
    public static RouteGroupBuilder RefundPaymentGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{paymentId:guid}/refund",
                async (Guid paymentId, [FromBody] RefundBody body, HttpContext httpContext, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<RefundPayment.RefundPaymentResponse>>(
                        new RefundPayment.RefundPaymentCommand(paymentId, body.RefundAmount,
                            httpContext.Connection.RemoteIpAddress?.ToString()));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("RefundPayment")
            .MapToApiVersion(1, 0)
            .Produces<RefundPayment.RefundPaymentResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }

    public record RefundBody(decimal RefundAmount);
}