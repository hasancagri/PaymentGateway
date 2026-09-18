namespace Merchant.Api.Domains.Merchants.Features.Queries;

// 044 US2: hassas kişisel/finansal alanların (Email, GsmNumber, IdentityNumber, Iban, TaxNumber)
// admin düzlemindeki TEK okuma yüzeyi — Admin UI hassas-veri sayfası (dar BFF) tüketir.
// MerchantKey/SubMerchantKey bu uçta YOK (sır teslimi kapsam dışı; MerchantScoped GetMerchant'ta
// kalır). AdminPlaneOnly: claim'li merchant token'ı giremez.
public static class GetMerchantSensitive
{
    public record GetMerchantSensitiveQuery(Guid MerchantId);

    public class GetMerchantSensitiveResponse
    {
        public Guid MerchantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string GsmNumber { get; set; } = string.Empty;
        public string? IdentityNumber { get; set; }
        public string Iban { get; set; } = string.Empty;
        public string? TaxNumber { get; set; }
    }

    public class GetMerchantSensitiveQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetMerchantSensitiveResponse>> Handle(
            GetMerchantSensitiveQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(query.MerchantId, ct);
            if (merchant is null || merchant.IsDeleted)
                return FeatureObjectResultModel<GetMerchantSensitiveResponse>.NotFound();

            return FeatureObjectResultModel<GetMerchantSensitiveResponse>.Ok(new GetMerchantSensitiveResponse
            {
                MerchantId = merchant.Id,
                Name = merchant.Name,
                Type = merchant.Type.ToString(),
                Email = merchant.Email,
                GsmNumber = merchant.GsmNumber,
                IdentityNumber = merchant.IdentityNumber,
                Iban = merchant.Iban,
                TaxNumber = merchant.TaxNumber
            });
        }
    }
}

public static class GetMerchantSensitiveEndpoint
{
    public static RouteGroupBuilder GetMerchantSensitiveGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{merchantId:guid}/sensitive",
                async (Guid merchantId, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<GetMerchantSensitive.GetMerchantSensitiveResponse>>(
                            new GetMerchantSensitive.GetMerchantSensitiveQuery(merchantId));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
                })
            .WithName("GetMerchantSensitive")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantRead, AuthorizationPolicies.AdminPlaneOnly)
            .Produces<GetMerchantSensitive.GetMerchantSensitiveResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}