using Grpc.Core;
using PaymentGateway.BankContracts;

namespace GarantiService.Services;

public class GarantiPaymentService : BankPaymentService.BankPaymentServiceBase
{
    public override Task<AuthResponse> Auth(AuthRequest request, ServerCallContext context)
    {
        return Task.FromResult(new AuthResponse
        {
            IsApproved = true,
            ResultCode = "00",
            Message = "Approved",
            BankTransactionId = Guid.NewGuid().ToString()
        });
    }
}