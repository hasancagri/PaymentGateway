namespace Payment.Api.Domains.PaymentSessions.Features.Agents;

/// <summary>
/// Faz 2 (select) — agent'a açık slice. Kullanıcının seçtiği taksiti oturuma yazar; seçim yalnız
/// Faz 1'de sunulanlardan olabilir (FR-012), oturum quote fazında olmalı (FR-017). <b>Çekim
/// YAPILMAZ</b> — oturum <see cref="PaymentSessionStatus.InstallmentSelected"/> fazında durur;
/// sonraki pay feature'ı bu oturumu okuyup çekimi kurar (plan §Seam).
/// </summary>
public static class SelectInstallment
{
    public record SelectInstallmentCommand(Guid SessionId, int InstallmentCount);

    public class SelectInstallmentResponse
    {
        public Guid SessionId { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? SelectedInstallmentCount { get; set; }

        public static SelectInstallmentResponse From(PaymentSession session) => new()
        {
            SessionId = session.Id,
            Status = session.Status.ToString(),
            SelectedInstallmentCount = session.SelectedInstallmentCount
        };
    }

    [Transactional]
    public class SelectInstallmentCommandHandler
    {
        public async Task<FeatureObjectResultModel<SelectInstallmentResponse>> Handle(
            SelectInstallmentCommand command,
            IDocumentSession session,
            CancellationToken ct)
        {
            var paymentSession = await session.LoadAsync<PaymentSession>(command.SessionId, ct);
            if (paymentSession is null)
                return FeatureObjectResultModel<SelectInstallmentResponse>.NotFound();

            var result = paymentSession.SelectInstallment(command.InstallmentCount);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<SelectInstallmentResponse>.Error(result.Messages);

            session.Store(paymentSession);

            return FeatureObjectResultModel<SelectInstallmentResponse>.Ok(
                SelectInstallmentResponse.From(paymentSession));
        }
    }
}