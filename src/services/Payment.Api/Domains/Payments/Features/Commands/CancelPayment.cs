using CP.VPOS;
using CP.VPOS.Enums;
using CP.VPOS.Models;

namespace Payment.Api.Domains.Payments.Features.Commands;

public static class CancelPayment
{
    public record CancelPaymentCommand(Guid PaymentId, string? CustomerIpAddress);

    public class CancelPaymentResponse
    {
        public Guid PaymentId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
    }

    [Transactional]
    public class CancelPaymentCommandHandler
    {
        public async Task<FeatureObjectResultModel<CancelPaymentResponse>> Handle(
            CancelPaymentCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var payment = await session.LoadAsync<Payment>(cmd.PaymentId, ct);
            if (payment is null || payment.IsDeleted)
                return FeatureObjectResultModel<CancelPaymentResponse>.NotFound();

            if (payment.BankCode is null || payment.TransactionId is null)
            {
                return FeatureObjectResultModel<CancelPaymentResponse>.Error(new MessageItem
                {
                    Property = nameof(payment.TransactionId),
                    Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR
                });
            }

            var account = await session.Query<PosAccount>()
                .Where(a => a.BankCode == payment.BankCode && a.IsActive && !a.IsDeleted)
                .FirstOrDefaultAsync(ct);
            if (account is null)
                return FeatureObjectResultModel<CancelPaymentResponse>.NotFound();

            var cancelResponse = VPOSClient.Cancel(new CancelRequest
            {
                customerIPAddress = cmd.CustomerIpAddress ?? "0.0.0.0",
                orderNumber = payment.OrderNumber,
                transactionId = payment.TransactionId,
                currency = Currency.TRY
            }, ProcessPayment.ProcessPaymentCommandHandler.BuildAuth(account));

            if (cancelResponse.statu != ResponseStatu.Success)
            {
                return FeatureObjectResultModel<CancelPaymentResponse>.Error(new MessageItem
                {
                    Property = nameof(payment.Status),
                    Code = cancelResponse.message
                });
            }

            var cancelResult = payment.Cancel();
            if (!cancelResult.IsSuccess)
                return FeatureObjectResultModel<CancelPaymentResponse>.Error(cancelResult.Messages);

            session.Store(payment);

            return FeatureObjectResultModel<CancelPaymentResponse>.Ok(new CancelPaymentResponse
            {
                PaymentId = payment.Id,
                Status = payment.Status.ToString(),
                Message = cancelResponse.message
            });
        }
    }
}

public static class CancelPaymentCommandEndpoint
{
    public static RouteGroupBuilder CancelPaymentGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{paymentId:guid}/cancel",
                async (Guid paymentId, HttpContext httpContext, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<CancelPayment.CancelPaymentResponse>>(
                        new CancelPayment.CancelPaymentCommand(paymentId,
                            httpContext.Connection.RemoteIpAddress?.ToString()));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("CancelPayment")
            .MapToApiVersion(1, 0)
            .Produces<CancelPayment.CancelPaymentResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}