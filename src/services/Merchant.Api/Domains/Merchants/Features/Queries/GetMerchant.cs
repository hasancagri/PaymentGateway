
namespace Merchant.Api.Domains.Merchants.Features.Queries;

public static class GetMerchant
{
    public record GetMerchantQuery(Guid Id);

    public class GetMerchantResponse
    {
        public Guid Id { get; set; }
        public string MerchantKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string CountryCode { get; set; } = string.Empty;
        public string? CountryName { get; set; }
        public string CityCode { get; set; } = string.Empty;
        public string? CityName { get; set; }
        public string Mcc { get; set; } = string.Empty;
        public string? MccName { get; set; }
        public string WebhookUrl { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        /// <summary>013: ödeme dönüş adresi (Active koşulu #3).</summary>
        public string? ReturnUrl { get; set; }

        /// <summary>013: merchant'ın başvuruda verdiği iletişim maili.</summary>
        public string? MerchantMail { get; set; }

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
                .Where(m => m.Id == query.Id && !m.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (merchant is null)
                return FeatureObjectResultModel<GetMerchantResponse>.NotFound();

            // İsim zenginleştirme: Reference olaylarıyla beslenen yerel read-model'den (id = Code).
            var country = await session.LoadAsync<ReferenceCountry>(ReferenceKey.Country(merchant.CountryCode), ct);
            var city = await session.LoadAsync<ReferenceCity>(merchant.CityCode, ct);
            var mcc = await session.LoadAsync<ReferenceMcc>(merchant.Mcc, ct);

            return FeatureObjectResultModel<GetMerchantResponse>.Ok(new GetMerchantResponse
            {
                Id = merchant.Id,
                MerchantKey = merchant.MerchantKey,
                Name = merchant.Name,
                Email = merchant.Email,
                Phone = merchant.Phone,
                CountryCode = merchant.CountryCode,
                CountryName = country?.Name,
                CityCode = merchant.CityCode,
                CityName = city?.Name,
                Mcc = merchant.Mcc,
                MccName = mcc?.Name,
                WebhookUrl = merchant.WebhookUrl,
                Status = merchant.Status.ToString(),
                ReturnUrl = merchant.ReturnUrl,
                MerchantMail = merchant.MerchantMail,
                CreatedTime = merchant.CreatedTime
            });
        }
    }
}

public static class GetMerchantQueryEndpoint
{
    public static RouteGroupBuilder GetMerchantGroupItemEndpoint(this RouteGroupBuilder group)
    {
        // 012: route parametresi {merchantId} — MerchantScoped policy tek anahtardan okur (D7).
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