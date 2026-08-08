namespace Merchant.Api.Domains.RegisterRequests.Features.Queries;

/// <summary>US2 — admin "Merchant Talepleri" listesi (opsiyonel statü filtresi; varsayılan Pending).</summary>
public static class GetRegisterRequests
{
    public record GetRegisterRequestsQuery(RegisterRequestStatus? Status);

    public class GetRegisterRequestsResponse
    {
        public List<RegisterRequestListItem> Items { get; set; } = new();
    }

    public class RegisterRequestListItem
    {
        public Guid Id { get; set; }
        public string Domain { get; set; } = string.Empty;
        public string LegalName { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string ChallengeResult { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; }
    }

    public class GetRegisterRequestsQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetRegisterRequestsResponse>> Handle(
            GetRegisterRequestsQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var q = session.Query<RegisterRequest>().AsQueryable();
            if (query.Status is not null)
                q = q.Where(r => r.Status == query.Status);

            var items = (await q.OrderByDescending(r => r.CreatedTime).ToListAsync(ct))
                .Select(r => new RegisterRequestListItem
                {
                    Id = r.Id,
                    Domain = r.Domain,
                    LegalName = r.LegalName,
                    ContactEmail = r.ContactEmail,
                    ChallengeResult = r.ChallengeResult.ToString(),
                    Status = r.Status.ToString(),
                    CreatedTime = r.CreatedTime
                })
                .ToList();

            return FeatureObjectResultModel<GetRegisterRequestsResponse>.Ok(new GetRegisterRequestsResponse
            {
                Items = items
            });
        }
    }
}
