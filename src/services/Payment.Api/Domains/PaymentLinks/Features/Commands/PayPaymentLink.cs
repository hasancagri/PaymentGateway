using Payment.Api.Domains.BinCards;
using Payment.Api.Domains.PaymentLinks;
using Payment.Api.Domains.PaymentLinks.BankCharge;

namespace Payment.Api.Domains.PaymentLinks.Features.Commands;

/// <summary>
/// 090 US1/US2 (FR-001..FR-013): Müşteri "Öde"ye basar. Kart yalnız bu komutta taşınır, SAKLANMAZ (İlke VI —
/// komut InvokeAsync-only, PublishAsync DEĞİL → PAN outbox'a girmez). Akış: link yükle → ödenebilirlik guard →
/// BIN çöz + router 3 simüle banka arasında aday sırala + failover (Technical/Config → sıradaki; Issuer/Fraud →
/// dur) → her denemeyi RecordAttempt → onayda Paid + PaymentCompletedEvent + store imzalı webhook; issuer/fraud
/// reddinde link Opened kalır + CHARGE_DECLINED + PaymentFailedEvent + webhook; tüm bankalar technical →
/// PROVIDER_UNAVAILABLE (link Opened, olay/webhook YOK — geçici, tekrar denenebilir). Durum+olay+webhook aynı
/// [Transactional] (FR-011 atomik).
/// </summary>
public static class PayPaymentLink
{
    public record PayPaymentLinkCommand(string OpenToken, CardData Card);

    public class PayPaymentLinkResponse
    {
        public string Status { get; set; } = string.Empty;
        public string RedirectUrl { get; set; } = string.Empty;
    }

    [Transactional]
    public class PayPaymentLinkCommandHandler
    {
        public async Task<FeatureObjectResultModel<PayPaymentLinkResponse>> Handle(
            PayPaymentLinkCommand cmd, IDocumentSession session, IMessageBus bus, BankChargeRouter router,
            PaymentLinkOptions options, StoreCallbackOptions callbackOptions, CancellationToken ct)
        {
            var now = DateTime.UtcNow;

            var link = await session.Query<PaymentLink>()
                .Where(x => x.OpenToken == cmd.OpenToken)
                .FirstOrDefaultAsync(ct);

            if (link is null)
                return FeatureObjectResultModel<PayPaymentLinkResponse>.Error(
                    new MessageItem { Code = PaymentLinkResourceConstants.LINK_NOT_FOUND });

            // Ödenebilirlik ön-kontrolü (lazy Expired flip olabilir → kalıcılaştır).
            var payable = link.IsPayable(now, options.GraceWindow);
            if (!payable.IsSuccess)
            {
                session.Store(link);
                return FeatureObjectResultModel<PayPaymentLinkResponse>.Error(payable.Messages);
            }

            // Çekim isteği (kart transient — saklanmaz/loglanmaz, İlke VI).
            var request = new BankChargeRequest(cmd.Card, link.Amount, link.Currency, link.OrderRef, link.Id);
            var binCards = await session.Query<BinCard>().ToListAsync(ct);

            var attempts = await router.RouteAsync(request, binCards, ct);

            // Aday yok / hepsi yönlendirilemez hata → geçici "sağlayıcı yok" (link Opened kalır, tekrar denenebilir).
            if (attempts.Count == 0)
                return FeatureObjectResultModel<PayPaymentLinkResponse>.Error(
                    new MessageItem { Code = PaymentLinkResourceConstants.PROVIDER_UNAVAILABLE });

            // Denenen her bankayı denetim kaydına geçir (onay en sonda → Paid'i flip eder).
            foreach (var attempt in attempts)
            {
                var recorded = link.RecordAttempt(attempt.BankCode, attempt.Outcome, now, options.GraceWindow);
                if (!recorded.IsSuccess)
                    return FeatureObjectResultModel<PayPaymentLinkResponse>.Error(recorded.Messages);
            }
            session.Store(link);

            var deciding = attempts[^1];

            // Onay → Paid + tamamlandı olayı + store imzalı webhook (terminal sonuç).
            if (deciding.Outcome.IsApproved)
            {
                await bus.PublishAsync(new IntegrationEvents.PaymentCompletedEvent(link.Id, link.OrderRef, link.Amount, deciding.BankCode));
                await bus.PublishAsync(new StoreResultDelivery.Deliver(
                    callbackOptions.CallbackUrl, link.Id.ToString(), link.OrderRef, nameof(PaymentLinkStatus.Paid),
                    link.Amount, link.Currency, deciding.BankCode, null));

                return FeatureObjectResultModel<PayPaymentLinkResponse>.Ok(new PayPaymentLinkResponse
                {
                    Status = nameof(PaymentLinkStatus.Paid),
                    RedirectUrl = link.ReturnUrl
                });
            }

            // Issuer/Fraud reddi → gerçek red verdict'i: CHARGE_DECLINED + başarısız olay + webhook (link Opened).
            if (deciding.Outcome.DeclineClass is DeclineClass.IssuerDecline or DeclineClass.FraudDecline)
            {
                await bus.PublishAsync(new IntegrationEvents.PaymentFailedEvent(
                    link.Id, link.OrderRef, link.Amount, PaymentLinkResourceConstants.CHARGE_DECLINED));
                await bus.PublishAsync(new StoreResultDelivery.Deliver(
                    callbackOptions.CallbackUrl, link.Id.ToString(), link.OrderRef, "Failed",
                    link.Amount, link.Currency, deciding.BankCode, PaymentLinkResourceConstants.CHARGE_DECLINED));

                return FeatureObjectResultModel<PayPaymentLinkResponse>.Error(
                    new MessageItem { Code = PaymentLinkResourceConstants.CHARGE_DECLINED });
            }

            // Belirsiz (in-doubt: ağ hatası + durum-sorgu da ulaşamadı) → dayanıklı reconcile Process devralır
            // (çift-çekim emniyeti; AYNI OrderId'yi sonra tekrar sorgular). Link Opened kalır; çözüm async.
            if (deciding.Outcome.DeclineClass is DeclineClass.TechnicalFailure && deciding.Outcome.RawCode == "INDOUBT")
                await bus.PublishAsync(
                    new Process.InDoubtReconciliation.Check(link.Id, deciding.BankCode, deciding.Outcome.ProviderRef ?? string.Empty, 0),
                    new DeliveryOptions { ScheduleDelay = TimeSpan.FromSeconds(30) });

            // Tüm uygun bankalar Technical/Config → geçici: olay/webhook YOK, link Opened (müşteri tekrar dener).
            return FeatureObjectResultModel<PayPaymentLinkResponse>.Error(
                new MessageItem { Code = PaymentLinkResourceConstants.PROVIDER_UNAVAILABLE });
        }
    }
}
