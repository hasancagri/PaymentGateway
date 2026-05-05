using GarantiService.Credentials;
using Grpc.Core;
using PaymentGateway.BankContracts;

namespace GarantiService.Services;

public class GarantiPaymentService : BankPaymentService.BankPaymentServiceBase
{
    private readonly GarantiHttpClient _garantiHttpClient;
    private readonly GarantiBankDbContext _context;
    private readonly ILogger<GarantiPaymentService> _logger;

    public GarantiPaymentService(
        GarantiHttpClient garantiHttpClient,
        GarantiBankDbContext context,
        ILogger<GarantiPaymentService> logger)
    {
        _garantiHttpClient = garantiHttpClient;
        _context = context;
        _logger = logger;
    }

    public override async Task<AuthResponse> Auth(AuthRequest request, ServerCallContext context)
    {
        var merchantId = Guid.Parse(request.MerchantId);

        var credential = await _context.Credentials
            .FirstOrDefaultAsync(c => c.MerchantId == merchantId && c.Status == 1,
                context.CancellationToken);

        if (credential is null)
        {
            _logger.LogError("No active credentials found for merchant {MerchantId}", merchantId);
            throw new RpcException(new Status(StatusCode.NotFound, "Merchant credentials not found"));
        }

        try
        {
            var garantiResponse = await _garantiHttpClient.AuthAsync(
                request.CardNo, request.ExpiryMonth, request.ExpiryYear, request.Cvv2,
                request.Amount, request.Currency, request.CardHolderName, request.CardHolderIp,
                request.InstallmentCount, request.OrderId,
                credential.MerchantCode, credential.TerminalCode,
                context.CancellationToken);

            var responseCode = garantiResponse?.Transaction?.Response?.Code;
            var isApproved = responseCode == "00";

            var response = new AuthResponse
            {
                IsApproved = isApproved,
                BankTransactionId = garantiResponse?.Transaction?.TxnId ?? string.Empty,
                ResultCode = responseCode ?? "99",
                Message = isApproved
                    ? garantiResponse?.Transaction?.Response?.Message ?? string.Empty
                    : garantiResponse?.Transaction?.Response?.ErrorMessage ?? string.Empty
            };

            if (garantiResponse?.Transaction?.RetrefNum is { } retrefNum)
                response.AdditionalData["RetrefNum"] = retrefNum;

            if (garantiResponse?.Transaction?.AuthCode is { } authCode)
                response.AdditionalData["AuthCode"] = authCode;

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Garanti auth failed for order {OrderId}", request.OrderId);
            throw new RpcException(new Status(StatusCode.Unavailable, "Bank communication error"));
        }
    }
}