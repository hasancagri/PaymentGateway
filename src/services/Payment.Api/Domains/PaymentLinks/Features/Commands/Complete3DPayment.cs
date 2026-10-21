using Payment.Api.Options;
using Payment.Api.Domains.PaymentLinks.BankCharge;

namespace Payment.Api.Domains.PaymentLinks.Features.Commands;

/// <summary>
/// Phase-1 3DS (İlke VI/VII): banka ACS'ten sonra sonucu PG'nin 3d-callback ucuna auto-POST eder. PG:
/// (1) callback HASH'ini doğrular (banka imzası — sahte/değişmiş callback fail-closed reddedilir, İlke V/VI);
/// (2) 3DStatus!=1 → 3D doğrulama başarısız, çekim YOK (CHARGE_DECLINED); (3) 3DStatus=1 → 3DModel tamamlama
/// (`Complete3DAsync`, SecureType=NonSecure + MD) ile ASIL çekimi tetikler; onayda link Paid + PaymentCompleted
/// + imzalı store webhook; issuer/fraud reddinde Opened kalır + Failed webhook. Kart PG'ye hiç gelmedi.
/// </summary>
public static class Complete3DPayment
{
    public record Command(string OpenToken, string BankCode, IReadOnlyDictionary<string, string> Callback);

    public class Response
    {
        public string Status { get; set; } = string.Empty;
        public string RedirectUrl { get; set; } = string.Empty;
    }

    [Transactional]
    public class Handler
    {
        public async Task<FeatureObjectResultModel<Response>> Handle(
            Command cmd, IDocumentSession session, IMessageBus bus, IEnumerable<IBankPaymentAdapter> adapters,
            PaymentLinkOptions options, StoreCallbackOptions callbackOptions, CancellationToken ct)
        {
            var now = DateTime.UtcNow;

            var link = await session.Query<PaymentLink>().Where(x => x.OpenToken == cmd.OpenToken).FirstOrDefaultAsync(ct);
            if (link is null)
                return FeatureObjectResultModel<Response>.Error(new MessageItem { Code = PaymentLinkResourceConstants.LINK_NOT_FOUND });

            var payable = link.IsPayable(now, options.GraceWindow);
            if (!payable.IsSuccess)
            {
                session.Store(link);
                return FeatureObjectResultModel<Response>.Error(payable.Messages);
            }

            // Callback'i gönderdiği banka adapter'ı (bank query-string'inden) + 3D yeteneği.
            if (adapters.FirstOrDefault(a => a.BankCode == cmd.BankCode) is not IThreeDCapableAdapter td)
                return FeatureObjectResultModel<Response>.Error(new MessageItem { Code = PaymentLinkResourceConstants.PROVIDER_UNAVAILABLE });

            // İmza doğrulama — sahte/değişmiş callback fail-closed (İlke V/VI). Olay/webhook YOK.
            if (!td.VerifyCallback(cmd.Callback))
                return FeatureObjectResultModel<Response>.Error(new MessageItem { Code = PaymentLinkResourceConstants.CHARGE_DECLINED });

            var orderId = cmd.Callback.GetValueOrDefault("OrderId", string.Empty);

            // 3DStatus != 1 → kimlik doğrulama başarısız, çekim YOK → gerçek red verdict'i (+Failed webhook).
            if (cmd.Callback.GetValueOrDefault("3DStatus") != "1")
            {
                link.RecordAttempt(cmd.BankCode,
                    BankChargeOutcome.Declined(DeclineClass.IssuerDecline, "3D-FAIL", orderId) with { BankOrderId = orderId },
                    now, options.GraceWindow);
                session.Store(link);
                await PublishFailed(bus, link, cmd.BankCode, callbackOptions);
                return FeatureObjectResultModel<Response>.Error(new MessageItem { Code = PaymentLinkResourceConstants.CHARGE_DECLINED });
            }

            // 3D OK → asıl çekim (3DModel completion: SecureType=NonSecure + MD).
            var md = cmd.Callback.GetValueOrDefault("MD", string.Empty);
            var result = await td.Complete3DAsync(orderId, md, ct);
            var outcome = result.Data!;

            var recorded = link.RecordAttempt(cmd.BankCode, outcome, now, options.GraceWindow);
            if (!recorded.IsSuccess)
                return FeatureObjectResultModel<Response>.Error(recorded.Messages);
            session.Store(link);

            if (outcome.IsApproved)
            {
                await bus.PublishAsync(new IntegrationEvents.PaymentCompletedEvent(link.Id, link.OrderRef, link.Amount, cmd.BankCode));
                await bus.PublishAsync(new StoreResultDelivery.Deliver(
                    callbackOptions.CallbackUrl, link.Id.ToString(), link.OrderRef, nameof(PaymentLinkStatus.Paid),
                    link.Amount, link.Currency, cmd.BankCode, null));
                return FeatureObjectResultModel<Response>.Ok(new Response
                {
                    Status = nameof(PaymentLinkStatus.Paid),
                    RedirectUrl = link.ReturnUrl
                });
            }

            await PublishFailed(bus, link, cmd.BankCode, callbackOptions);
            return FeatureObjectResultModel<Response>.Error(new MessageItem { Code = PaymentLinkResourceConstants.CHARGE_DECLINED });
        }

        // Başarısız 3D sonucu → store'a imzalı webhook + PaymentFailed olayı (NonSecure akışıyla aynı sözleşme).
        private static async Task PublishFailed(IMessageBus bus, PaymentLink link, string bankCode, StoreCallbackOptions o)
        {
            await bus.PublishAsync(new IntegrationEvents.PaymentFailedEvent(
                link.Id, link.OrderRef, link.Amount, PaymentLinkResourceConstants.CHARGE_DECLINED));
            await bus.PublishAsync(new StoreResultDelivery.Deliver(
                o.CallbackUrl, link.Id.ToString(), link.OrderRef, "Failed",
                link.Amount, link.Currency, bankCode, PaymentLinkResourceConstants.CHARGE_DECLINED));
        }
    }
}
