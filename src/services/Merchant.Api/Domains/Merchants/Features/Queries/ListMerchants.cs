namespace Merchant.Api.Domains.Merchants.Features.Queries;

// 023: merchant listesi — tam liste (sayfalama yok, R7). AdminPlaneOnly: merchant token'ı
// tüm listeyi göremez (tenant sınırı). Yanıt tipinde MerchantKey alanı HİÇ YOK (SC-004).
// Wrapper response: boş liste geçerli sonuçtur (FeatureListResultModel boşta NotFound döner — kullanılmaz).
public static class ListMerchants
{
    public record ListMerchantsQuery;

    public class ListMerchantsResponse
    {
        public List<MerchantItem> Merchants { get; set; } = new();
    }

    public class MerchantItem
    {
        public Guid MerchantId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string GsmNumber { get; set; } = string.Empty;
        public string Iban { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string ContactSurname { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; }
    }

    public class ListMerchantsQueryHandler
    {
        public async Task<FeatureObjectResultModel<ListMerchantsResponse>> Handle(
            ListMerchantsQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var merchants = await session.Query<Merchant>()
                .Where(m => !m.IsDeleted)
                .OrderByDescending(m => m.CreatedTime)
                .ToListAsync(ct);

            return FeatureObjectResultModel<ListMerchantsResponse>.Ok(new ListMerchantsResponse
            {
                Merchants = merchants.Select(m => new MerchantItem
                {
                    MerchantId = m.Id,
                    Status = m.Status.ToString(),
                    Type = m.Type.ToString(),
                    Name = m.Name,
                    Email = m.Email,
                    GsmNumber = m.GsmNumber,
                    Iban = m.Iban,
                    ContactName = m.ContactName,
                    ContactSurname = m.ContactSurname,
                    CreatedTime = m.CreatedTime
                }).ToList()
            });
        }
    }
}

public static class ListMerchantsEndpoint
{
    public static RouteGroupBuilder ListMerchantsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/",
                async (IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<ListMerchants.ListMerchantsResponse>>(
                            new ListMerchants.ListMerchantsQuery());
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("ListMerchants")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantRead, AuthorizationPolicies.AdminPlaneOnly)
            .Produces<ListMerchants.ListMerchantsResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}