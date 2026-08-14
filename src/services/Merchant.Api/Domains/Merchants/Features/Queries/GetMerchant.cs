namespace Merchant.Api.Domains.Merchants.Features.Queries;

// 023: tekil merchant görüntüleme. MerchantScoped: merchant kendi token'ıyla yalnız kendi kaydını okur.
// SC-004 bilinçli delindi (dev kararı, 2026-08-14): MerchantKey yanıtla döner — Admin ekranı açık
// gösterir, ECommerce tarafına elle taşınır. Redeem-link teslim modeli gelince bu alan kaldırılacak.
public static class GetMerchant
{
    public record GetMerchantQuery(Guid MerchantId);

    public class GetMerchantResponse
    {
        public Guid MerchantId { get; set; }
        public string MerchantKey { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string GsmNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Iban { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string ContactSurname { get; set; } = string.Empty;
        public string? IdentityNumber { get; set; }
        public string? TaxOffice { get; set; }
        public string? TaxNumber { get; set; }
        public string? LegalCompanyTitle { get; set; }
        public string? SubMerchantKey { get; set; }
        public DateTime CreatedTime { get; set; }
    }

    public class GetMerchantQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetMerchantResponse>> Handle(
            GetMerchantQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var merchant = await session.Query<Merchant>()
                .Where(m => m.Id == query.MerchantId && !m.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (merchant is null)
                return FeatureObjectResultModel<GetMerchantResponse>.NotFound();

            return FeatureObjectResultModel<GetMerchantResponse>.Ok(new GetMerchantResponse
            {
                MerchantId = merchant.Id,
                MerchantKey = merchant.MerchantKey,
                Status = merchant.Status.ToString(),
                Type = merchant.Type.ToString(),
                Name = merchant.Name,
                Email = merchant.Email,
                GsmNumber = merchant.GsmNumber,
                Address = merchant.Address,
                Iban = merchant.Iban,
                ContactName = merchant.ContactName,
                ContactSurname = merchant.ContactSurname,
                IdentityNumber = merchant.IdentityNumber,
                TaxOffice = merchant.TaxOffice,
                TaxNumber = merchant.TaxNumber,
                LegalCompanyTitle = merchant.LegalCompanyTitle,
                SubMerchantKey = merchant.SubMerchantKey,
                CreatedTime = merchant.CreatedTime
            });
        }
    }
}

public static class GetMerchantEndpoint
{
    public static RouteGroupBuilder GetMerchantGroupItemEndpoint(this RouteGroupBuilder group)
    {
        // 012: route parametresi {merchantId} — MerchantScoped policy claim eşleşmesini buradan okur.
        group.MapGet("/{merchantId:guid}",
                async (Guid merchantId, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<GetMerchant.GetMerchantResponse>>(
                            new GetMerchant.GetMerchantQuery(merchantId));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
                })
            .WithName("GetMerchant")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantRead, AuthorizationPolicies.MerchantScoped)
            .Produces<GetMerchant.GetMerchantResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}
