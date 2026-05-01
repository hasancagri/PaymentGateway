using PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Enums;
using PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.ValueObjects;
using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Features.Commands;

public static class InitiatePayment
{
    public class InitiatePaymentCommand
    {
        public required Guid MerchantId { get; set; }
        public required string OrderId { get; set; }
        public required TransactionType Type { get; set; }
        public required decimal Amount { get; set; }
        public required string Currency { get; set; }
        public required string EncryptedCardNumber { get; set; }
        public required string CardHolderName { get; set; }
        public required string ExpiryMonth { get; set; }
        public required string ExpiryYear { get; set; }
        public required string CardHolderIp { get; set; }
    }

    public class InitiatePaymentResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class InitiatePaymentHandler
    {
        public async Task<FeatureObjectResultModel<InitiatePaymentResponse>> Handle(
            InitiatePaymentCommand cmd,
            PaymentProcessingContext db,
            CancellationToken ct)
        {
            var txResult = PaymentTransaction.Initiate(
                cmd.MerchantId, cmd.OrderId, cmd.Type,
                cmd.Amount, cmd.Currency,
                cmd.EncryptedCardNumber, cmd.CardHolderName,
                cmd.ExpiryMonth, cmd.ExpiryYear, cmd.CardHolderIp);

            if (!txResult.IsSuccess)
                return FeatureObjectResultModel<InitiatePaymentResponse>.Error(txResult.Messages!);

            await db.Set<PaymentTransaction>().AddAsync(txResult.Data!, ct);
            return FeatureObjectResultModel<InitiatePaymentResponse>.Ok(new InitiatePaymentResponse { Id = txResult.Data!.Id });
        }
    }
}