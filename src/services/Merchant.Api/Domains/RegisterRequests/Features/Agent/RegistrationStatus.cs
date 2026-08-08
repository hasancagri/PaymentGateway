namespace Merchant.Api.Domains.RegisterRequests.Features.Agent;

/// <summary>US1 (opsiyonel) — domain için başvurunun güncel durumunu döner (Pending/Approved/Rejected).</summary>
public static class RegistrationStatus
{
    public record RegistrationStatusQuery(string Domain);

    public class RegistrationStatusResponse
    {
        public string Status { get; set; } = string.Empty;
        public Guid? RequestId { get; set; }
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
                RequestId = request.Id
            });
        }
    }
}