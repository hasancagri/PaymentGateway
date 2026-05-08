using System.Globalization;
using Marten;
using PaymentGateway.BankContracts;
using PaymentProcessing.Api.Modules.PaymentProcessing.BinRecords;
using PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Enums;
using PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Middleware;
using PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Services.BankAdapters;
using PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Services.BankAdapters.Abstractions;
using PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Services.Encryption;
using PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.ValueObjects;

namespace PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Features.Commands;

public static class AuthPayment
{
    [RequiresMerchant]
    public class AuthPaymentCommand
    {
        public required string OrderId { get; set; }
        public required string CardNo { get; set; }
        public required string CardHolderName { get; set; }
        public required string ExpiryMonth { get; set; }
        public required string ExpiryYear { get; set; }
        public required string Cvv2 { get; set; }
        public required decimal Amount { get; set; }
        public required string Currency { get; set; }
        public required string CardHolderIp { get; set; }
        public int InstallmentCount { get; set; } = 1;
    }

    public class AuthPaymentResponse
    {
        public Guid TransactionId { get; set; }
    }

    public class AuthPaymentHandler(ICardEncryptionService cardEncryption)
    {
        public async Task<FeatureObjectResultModel<AuthPaymentResponse>> Handle(
            AuthPaymentCommand cmd,
            MerchantIdentity merchant,
            IBankSelector bankSelector,
            BankRouter bankRouter,
            IDocumentSession session,
            CancellationToken ct)
        {
            var parsedOrderId = OrderId.Create(cmd.OrderId);
            if (!parsedOrderId.IsSuccess)
                return FeatureObjectResultModel<AuthPaymentResponse>.Error(parsedOrderId.Messages!);

            var existing = await Marten.QueryableExtensions.FirstOrDefaultAsync(
                session.Query<PaymentTransaction>()
                    .Where(t => t.MerchantId == merchant.MerchantId && t.OrderId == cmd.OrderId),
                ct);

            if (existing is not null)
            {
                return existing.Status switch
                {
                    TransactionStatus.Pending => FeatureObjectResultModel<AuthPaymentResponse>.Error(
                        new MessageItem { Code = "Payment.InProgress" }),
                    TransactionStatus.Failed => FeatureObjectResultModel<AuthPaymentResponse>.Error(
                        new MessageItem { Code = "Payment.PreviousAttemptFailed" }),
                    _ => FeatureObjectResultModel<AuthPaymentResponse>.Ok(
                        new AuthPaymentResponse { TransactionId = existing.Id })
                };
            }

            // BIN çözümleme
            var rawCard = cmd.CardNo.Replace(" ", "");
            if (rawCard.Length < 8)
                return FeatureObjectResultModel<AuthPaymentResponse>.Error(
                    new MessageItem { Code = "BinRecord.CardNumberTooShort" });

            var binAsLong = long.Parse(rawCard[..8]);
            var binRecord = await Marten.QueryableExtensions.FirstOrDefaultAsync(
                session.Query<BinRecord>()
                    .Where(b => b.BinEightStart <= binAsLong && binAsLong <= b.BinEightEnd),
                ct);

            if (binRecord is null)
                return FeatureObjectResultModel<AuthPaymentResponse>.Error(
                    new MessageItem { Code = "BinRecord.NotFound" });

            var profileResult = CardProfile.CreateFromBinRecord(binRecord);
            if (!profileResult.IsSuccess)
                return FeatureObjectResultModel<AuthPaymentResponse>.Error(profileResult.Messages!);

            var cardProfile = profileResult.Data!;

            var encryptedCard = cardEncryption.Encrypt(cmd.CardNo);
            var route = await bankSelector.SelectBestAsync(merchant.MerchantId, cmd.Currency, cardProfile, ct);
            var transactionId = Guid.NewGuid();

            session.Events.StartStream<PaymentTransaction>(transactionId, new PaymentInitiated(
                transactionId,
                merchant.MerchantId,
                cmd.OrderId,
                cmd.Amount,
                cmd.Currency,
                encryptedCard,
                cmd.CardHolderName,
                cmd.ExpiryMonth,
                cmd.ExpiryYear,
                cmd.CardHolderIp,
                route.BankId,
                route.BankRate,
                route.MerchantRate,
                cmd.InstallmentCount
            ));

            try
            {
                var grpcResponse = await bankRouter.Route(route.BankName).AuthAsync(new AuthRequest
                {
                    CardNo = cmd.CardNo,
                    ExpiryMonth = cmd.ExpiryMonth,
                    ExpiryYear = cmd.ExpiryYear,
                    Cvv2 = cmd.Cvv2,
                    Amount = cmd.Amount.ToString(CultureInfo.InvariantCulture),
                    Currency = cmd.Currency,
                    CardHolderName = cmd.CardHolderName,
                    CardHolderIp = cmd.CardHolderIp,
                    InstallmentCount = cmd.InstallmentCount,
                    MerchantId = merchant.MerchantId.ToString(),
                    OrderId = cmd.OrderId
                }, cancellationToken: ct);

                if (grpcResponse.IsApproved)
                {
                    var commissionResult = CommissionInfo.Create(cmd.Amount, route.BankRate, route.MerchantRate);
                    if (!commissionResult.IsSuccess)
                        return FeatureObjectResultModel<AuthPaymentResponse>.Error(commissionResult.Messages!);

                    var commission = commissionResult.Data!;
                    session.Events.Append(transactionId, new PaymentApproved(
                        transactionId,
                        merchant.MerchantId,
                        cmd.OrderId,
                        cmd.Amount,
                        cmd.Currency,
                        commission.MerchantAmount,
                        commission.BankAmount,
                        commission.NetAmount,
                        string.IsNullOrEmpty(grpcResponse.BankTransactionId) ? null : grpcResponse.BankTransactionId,
                        grpcResponse.ResultCode
                    ));
                }
                else
                {
                    session.Events.Append(transactionId, new PaymentDeclined(
                        transactionId,
                        merchant.MerchantId,
                        cmd.OrderId,
                        grpcResponse.ResultCode,
                        string.IsNullOrEmpty(grpcResponse.Message) ? null : grpcResponse.Message
                    ));
                }
            }
            catch (RpcException ex)
            {
                session.Events.Append(transactionId, new PaymentFailed(
                    transactionId,
                    merchant.MerchantId,
                    cmd.OrderId,
                    ex.Status.Detail
                ));
            }

            await session.SaveChangesAsync(ct);

            return FeatureObjectResultModel<AuthPaymentResponse>.Ok(
                new AuthPaymentResponse { TransactionId = transactionId });
        }
    }
}