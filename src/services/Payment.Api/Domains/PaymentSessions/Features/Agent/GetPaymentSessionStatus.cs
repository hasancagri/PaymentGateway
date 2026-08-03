namespace Payment.Api.Domains.PaymentSessions.Features.Agent;

/// <summary>
/// Story 3 (status) — agent'a açık slice. Oturumun güncel fazını döner (quote verildi / taksit
/// seçildi / başarısız). Salt-okuma; state değiştirmez. <see cref="PaymentSession.FailReason"/>
/// yalnız neden metnini taşır — kart verisi sızdırmaz.
/// </summary>
public static class GetPaymentSessionStatus
{
    public record GetPaymentSessionStatusQuery(Guid SessionId);

    public class GetPaymentSessionStatusResponse
    {
        public Guid SessionId { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? SelectedInstallmentCount { get; set; }
        public string? FailReason { get; set; }

        public static GetPaymentSessionStatusResponse From(PaymentSession session) => new()
        {
            SessionId = session.Id,
            Status = session.Status.ToString(),
            SelectedInstallmentCount = session.SelectedInstallmentCount,
            FailReason = session.FailReason
        };
    }

    public class GetPaymentSessionStatusQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetPaymentSessionStatusResponse>> Handle(
            GetPaymentSessionStatusQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var paymentSession = await session.LoadAsync<PaymentSession>(query.SessionId, ct);
            if (paymentSession is null)
                return FeatureObjectResultModel<GetPaymentSessionStatusResponse>.NotFound();

            return FeatureObjectResultModel<GetPaymentSessionStatusResponse>.Ok(
                GetPaymentSessionStatusResponse.From(paymentSession));
        }
    }
}