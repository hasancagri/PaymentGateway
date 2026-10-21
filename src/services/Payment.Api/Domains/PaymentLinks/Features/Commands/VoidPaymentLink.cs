using Payment.Api.Options;
using Payment.Api.Domains.PaymentLinks.BankCharge;

namespace Payment.Api.Domains.PaymentLinks.Features.Commands;

/// <summary>
/// Store linki iptal eder (Void — gün sonu öncesi). Link'in onaylı çekim denemesinin (BankCode + BankOrderId)
/// bankasına `VoidAsync` çağrılır; sonuç denetim kaydına (`RecordReversal`) yazılır + store'a webhook. Para
/// durumu bankada; PG yalnız iletir. Gün sonu geçtiyse banka reddeder → store Refund denemeli.
/// </summary>
public static class VoidPaymentLink
{
    public record Command(string PgPaymentRef);

    [Transactional]
    public class VoidPaymentLinkCommandHandler
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

            var result = await adapter.VoidAsync(approved.BankOrderId!, ct);
            var outcome = result.Data!;
            link.RecordReversal(approved.BankCode, outcome, DateTime.UtcNow);
            session.Store(link);

            if (!outcome.IsApproved)
                return FeatureResultModel.Error(new MessageItem { Code = PaymentLinkResourceConstants.REVERSAL_FAILED });

            await bus.PublishAsync(new StoreResultDelivery.Deliver(
                callbackOptions.CallbackUrl, link.Id.ToString(), link.OrderRef, "Voided",
                link.Amount, link.Currency, approved.BankCode, null));
            return FeatureResultModel.Ok();
        }
    }
}
