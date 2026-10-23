
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
    public record PayPaymentLinkCommand(string OpenToken, CardData Card, int InstallmentCount = 1);

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

            // BIN çözümü için TÜM tabloyu çekme (hot path). PAN'ın prefix'leri = aday BIN id'leri (Bin = Marten
            // PK) → yalnız onları key'le yükle (PK lookup). Prefix MaxBinLength'te kesilir → tam PAN asla sorgu
            // key'i olmaz (İlke VI). ResolveByPan en uzun eşleşeni seçer.
            var pan = cmd.Card.Pan;
            var binPrefixes = Enumerable.Range(1, Math.Min(pan.Length, BinCard.MaxBinLength))
                .Select(len => pan[..len])
                .ToArray();
            var binCards = await session.LoadManyAsync<BinCard>(ct, binPrefixes);
            var rates = await session.Query<BankCommissionRate>().ToListAsync(ct);

            // 093: seçilen taksitin en-avantajlı acquirer'ı + taksitli toplam SUNUCUDA hesaplanır (quote mantığının
            // aynısı — client'tan gelen banka/tutara GÜVENİLMEZ, FR-009). Uygun banka yoksa geçici PROVIDER_UNAVAILABLE.
            var installment = cmd.InstallmentCount < 1 ? 1 : cmd.InstallmentCount;
            var best = router.SelectCandidates(pan, binCards, link.Amount, installment, rates).FirstOrDefault();
            if (best is null)
                return FeatureObjectResultModel<PayPaymentLinkResponse>.Error(
                    new MessageItem { Code = PaymentLinkResourceConstants.PROVIDER_UNAVAILABLE });

            var finalAmount = Math.Round(link.Amount * (1 + best.EffectiveRate), 2, MidpointRounding.AwayFromZero);
            var applied = link.ApplyInstallment(installment, finalAmount, now, options.GraceWindow);
            if (!applied.IsSuccess)
            {
                session.Store(link);
                return FeatureObjectResultModel<PayPaymentLinkResponse>.Error(applied.Messages);
            }

            // Çekim isteği (kart transient — saklanmaz/loglanmaz, İlke VI). Tutar = taksitli toplam (FinalAmount).
            var request = new BankChargeRequest(cmd.Card, link.FinalAmount, link.Currency, link.OrderRef, link.Id, installment);

            // 093: FAILOVER YOK — yalnız o taksitin gösterilen (en-avantajlı) acquirer'ı denenir (gösterilen=çekilen).
            var attempts = await router.RouteAsync(request, binCards, rates, ct);

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

            var deciding = attempts.Last(); // router sözleşmesi: son deneme = karar veren

            // Onay → Paid + tamamlandı olayı + store imzalı webhook (terminal sonuç).
            if (deciding.Outcome.IsApproved)
            {
                await bus.PublishAsync(new IntegrationEvents.PaymentCompletedEvent(link.Id, link.OrderRef, link.FinalAmount, deciding.BankCode));
                await bus.PublishAsync(new StoreResultDelivery.Deliver(
                    callbackOptions.CallbackUrl, link.Id.ToString(), link.OrderRef, nameof(PaymentLinkStatus.Paid),
                    link.FinalAmount, link.Currency, deciding.BankCode, null));

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
                    link.Id, link.OrderRef, link.FinalAmount, PaymentLinkResourceConstants.CHARGE_DECLINED));
                await bus.PublishAsync(new StoreResultDelivery.Deliver(
                    callbackOptions.CallbackUrl, link.Id.ToString(), link.OrderRef, "Failed",
                    link.FinalAmount, link.Currency, deciding.BankCode, PaymentLinkResourceConstants.CHARGE_DECLINED));

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
