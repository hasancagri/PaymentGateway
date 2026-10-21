using Payment.Api.Domains.PaymentLinks.BankCharge;

namespace Payment.Api.Domains.PaymentLinks.Features.Commands;

/// <summary>
/// Store linke iade yapar (Refund — gün sonu sonrası, kısmi olabilir). Onaylı çekimin bankasına
/// `RefundAsync(bankOrderId, amount)` çağrılır; sonuç denetim kaydı + store webhook. Banka kısmi/aşım/komisyon
/// kurallarını uygular; PG iletir. Açık batch'te banka Void ister (red).
/// </summary>
public static class RefundPaymentLink
{
    public record Command(string PgPaymentRef, decimal Amount);

    [Transactional]
    public class RefundPaymentLinkCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            Command cmd, IDocumentSession session, IEnumerable<IBankPaymentAdapter> adapters,
            IMessageBus bus, StoreCallbackOptions callbackOptions, CancellationToken ct)
        {
            if (!Guid.TryParse(cmd.PgPaymentRef, out var id))
                return FeatureResultModel.Error(new MessageItem { Code = PaymentLinkResourceConstants.LINK_NOT_FOUND });

            var link = await session.LoadAsync<PaymentLink>(id, ct);
            if (link is null)
                return FeatureResultModel.Error(new MessageItem { Code = PaymentLinkResourceConstants.LINK_NOT_FOUND });

            var approved = link.Attempts.FirstOrDefault(a => a.IsApproved && !string.IsNullOrEmpty(a.BankOrderId));
            if (approved is null)
                return FeatureResultModel.Error(new MessageItem { Code = PaymentLinkResourceConstants.NO_REVERSIBLE_CHARGE });

            var adapter = adapters.FirstOrDefault(a => a.BankCode == approved.BankCode);
            if (adapter is null)
                return FeatureResultModel.Error(new MessageItem { Code = PaymentLinkResourceConstants.PROVIDER_UNAVAILABLE });

            var result = await adapter.RefundAsync(approved.BankOrderId!, cmd.Amount, ct);
            var outcome = result.Data!;
            link.RecordReversal(approved.BankCode, outcome, DateTime.UtcNow);
            session.Store(link);

            if (!outcome.IsApproved)
                return FeatureResultModel.Error(new MessageItem { Code = PaymentLinkResourceConstants.REVERSAL_FAILED });

            await bus.PublishAsync(new StoreResultDelivery.Deliver(
                callbackOptions.CallbackUrl, link.Id.ToString(), link.OrderRef, "Refunded",
                cmd.Amount, link.Currency, approved.BankCode, null));
            return FeatureResultModel.Ok();
        }
    }
}
