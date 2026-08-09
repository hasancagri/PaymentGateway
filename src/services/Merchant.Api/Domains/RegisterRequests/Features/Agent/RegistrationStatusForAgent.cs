namespace Merchant.Api.Domains.RegisterRequests.Features.Agent;

/// <summary>
/// US1 (opsiyonel) — domain için başvurunun güncel durumunu döner. 015: talep artık challenge'dan önce
/// (AwaitingDomainControl) kalıcı olduğundan bu sorgu o durumu da raporlar; <see cref="RegistrationStatusResponse.Message"/>
/// güncel durumu + sıradaki adımı Türkçe metinle bildirir (ECommerce "sürecim ne oldu?" — on-demand, poll zorunlu değil).
/// </summary>
public static class RegistrationStatusForAgent
{
    public record RegistrationStatusQuery(string Domain);

    public class RegistrationStatusResponse
    {
        /// <summary>Enum adı: AwaitingDomainControl / Pending / Approved / Rejected.</summary>
        public string Status { get; set; } = string.Empty;
        public Guid? RequestId { get; set; }

        /// <summary>015: güncel durum + sıradaki adım — insan-okur Türkçe metin.</summary>
        public string Message { get; set; } = string.Empty;
    }

    public class RegistrationStatusQueryHandler
    {
        public async Task<FeatureObjectResultModel<RegistrationStatusResponse>> Handle(
            RegistrationStatusQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var domain = query.Domain?.Trim().ToLowerInvariant() ?? string.Empty;

            var request = await session.Query<RegisterRequest>()
                .Where(r => r.Domain == domain)
                .OrderByDescending(r => r.CreatedTime)
                .FirstOrDefaultAsync(ct);

            if (request is null)
                return FeatureObjectResultModel<RegistrationStatusResponse>.NotFound();

            return FeatureObjectResultModel<RegistrationStatusResponse>.Ok(new RegistrationStatusResponse
            {
                Status = request.Status.ToString(),
                RequestId = request.Id,
                Message = StatusMessage(request.Status)
            });
        }

        // Durum → sıradaki adım metni (on-demand "sürecim ne oldu?" yanıtı).
        private static string StatusMessage(RegisterRequestStatus status) => status switch
        {
            RegisterRequestStatus.AwaitingDomainControl =>
                "Alan adı sahipliği bekleniyor: verilen challenge değerini belirtilen yola yayınlayıp başvuruyu tekrarlayın.",
            RegisterRequestStatus.Pending =>
                "Sahiplik doğrulandı; admin onayı bekleniyor.",
            RegisterRequestStatus.Approved =>
                "Başvuru onaylandı; aktivasyon adımına geçebilirsiniz (key teslim linki mail ile iletildi).",
            RegisterRequestStatus.Rejected =>
                "Başvuru reddedildi; aynı alan adı için yeniden başvurabilirsiniz.",
            _ => string.Empty
        };
    }
}