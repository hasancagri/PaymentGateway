namespace Payment.Api.Domains.PaymentLinks.Process;

/// <summary>
/// BC'nin KENDİ dayanıklı süreci (conventions `Process/`): in-doubt (belirsiz) çekimi çözer. Çekim ağ
/// hatasıyla cevapsız kaldı ve adapter durum-sorgu da ulaşamadıysa (RawCode=INDOUBT) PayPaymentLink bu
/// mesajı zamanlanmış yayınlar. Handler bankayı AYNI OrderId ile tekrar sorgular (çift-çekim emniyeti,
/// Ders 16): banka çekmişse link `Paid`'e kurtarılır (PaymentCompleted + store webhook); çekilmemişse
/// durur (müşteri normal akışta tekrar dener); hâlâ belirsizse sınırlı sayıda yeniden zamanlanır → tükenirse
/// bırakılır (gün-sonu mutabakatına kalır). Otorite = bankanın kaydı, HTTP cevabı değil.
/// </summary>
public static class InDoubtReconciliation
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    public record Check(Guid PaymentLinkId, string BankCode, string BankOrderId, int Attempt);

    [Transactional]
    public class CheckHandler
    {
        public async Task Handle(
            Check msg, IDocumentSession session, IEnumerable<IBankPaymentAdapter> adapters, IMessageBus bus,
            PaymentLinkOptions options, StoreCallbackOptions callbackOptions, CancellationToken ct)
        {
            var link = await session.LoadAsync<PaymentLink>(msg.PaymentLinkId, ct);
            if (link is null || link.IsTerminal) return; // çözülmüş/iptal/expired/paid → iş yok

            var adapter = adapters.FirstOrDefault(a => a.BankCode == msg.BankCode);
            if (adapter is null) return;

            var status = await adapter.QueryStatusAsync(msg.BankOrderId, ct);
            var outcome = status.Data;
            if (outcome is null) return;

            if (outcome.IsApproved)
            {
                // Banka çekmiş → onayı KURTAR (çift çekim önlendi; link gerçekten ödenmişti).
                var now = DateTime.UtcNow;
                var recorded = link.RecordAttempt(msg.BankCode, outcome, now, options.GraceWindow);
                if (!recorded.IsSuccess) return; // link bu arada expired/terminal → mutabakata kalır (escalate)

                session.Store(link);
                await bus.PublishAsync(new IntegrationEvents.PaymentCompletedEvent(
                    link.Id, link.OrderRef, link.Amount, msg.BankCode));
                await bus.PublishAsync(new StoreResultDelivery.Deliver(
                    callbackOptions.CallbackUrl, link.Id.ToString(), link.OrderRef,
                    nameof(PaymentLinkStatus.Paid), link.Amount, link.Currency, msg.BankCode, null));
                return;
            }

            // Hâlâ belirsiz (INDOUBT) → sınırlı yeniden dene; çözülmüş-çekilmemiş (NOTCHARGED) → dur.
            if (outcome.RawCode == "INDOUBT" && msg.Attempt + 1 < MaxAttempts)
                await bus.PublishAsync(
                    msg with { Attempt = msg.Attempt + 1 },
                    new DeliveryOptions { ScheduleDelay = RetryDelay });
            // NOTCHARGED ya da deneme hakkı bitti → sessizce dur (gün-sonu mutabakatı son emniyet).
        }
    }
}
