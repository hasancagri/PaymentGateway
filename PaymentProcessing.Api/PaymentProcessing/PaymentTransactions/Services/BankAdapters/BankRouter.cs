using Grpc.Net.ClientFactory;
using PaymentGateway.BankContracts;

namespace PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Services.BankAdapters;

public class BankRouter : IScopedDependency
{
    private readonly GrpcClientFactory _grpcClientFactory;

    public BankRouter(GrpcClientFactory grpcClientFactory)
        => _grpcClientFactory = grpcClientFactory;

    public BankPaymentService.BankPaymentServiceClient Route(string bankName)
        => _grpcClientFactory.CreateClient<BankPaymentService.BankPaymentServiceClient>(
            bankName.ToLowerInvariant());
}