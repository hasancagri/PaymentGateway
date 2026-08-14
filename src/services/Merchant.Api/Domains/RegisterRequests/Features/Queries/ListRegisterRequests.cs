namespace Merchant.Api.Domains.RegisterRequests.Features.Queries;

// 029 US2: admin listesi — tüm başvurular (tarihçe dahil), CreatedTime DESC; sayfalama yok
// (düşük hacim varsayımı). AdminPlaneOnly: merchant token'ı bu listeyi göremez.
public static class ListRegisterRequests
{
    public record ListRegisterRequestsQuery;

    public class ListRegisterRequestsResponse
    {
        public List<RegisterRequestItem> Requests { get; set; } = new();
    }

    public class RegisterRequestItem
    {
        public Guid RequestId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string GsmNumber { get; set; } = string.Empty;
        public string Iban { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string ContactSurname { get; set; } = string.Empty;
        public string? RejectReason { get; set; }
        public Guid? MerchantId { get; set; }
        public DateTime CreatedTime { get; set; }
    }

    public class ListRegisterRequestsQueryHandler
    {
        public async Task<FeatureObjectResultModel<ListRegisterRequestsResponse>> Handle(
            ListRegisterRequestsQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var requests = await session.Query<RegisterRequest>()
                .OrderByDescending(r => r.CreatedTime)
                .ToListAsync(ct);

            return FeatureObjectResultModel<ListRegisterRequestsResponse>.Ok(new ListRegisterRequestsResponse
            {
                Requests = requests.Select(r => new RegisterRequestItem
                {
                    RequestId = r.Id,
                    Status = r.Status.ToString(),
                    Type = r.Type.ToString(),
                    Name = r.Name,
                    Email = r.Email,
                    GsmNumber = r.GsmNumber,
                    Iban = r.Iban,
                    ContactName = r.ContactName,
                    ContactSurname = r.ContactSurname,
                    RejectReason = r.RejectReason,
                    MerchantId = r.MerchantId,
                    CreatedTime = r.CreatedTime
                }).ToList()
            });
        }
    }
}

public static class ListRegisterRequestsEndpoint
{
    public static RouteGroupBuilder ListRegisterRequestsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/",
                async (IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<ListRegisterRequests.ListRegisterRequestsResponse>>(
                            new ListRegisterRequests.ListRegisterRequestsQuery());
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("ListRegisterRequests")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantRead, AuthorizationPolicies.AdminPlaneOnly)
            .Produces<ListRegisterRequests.ListRegisterRequestsResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}
