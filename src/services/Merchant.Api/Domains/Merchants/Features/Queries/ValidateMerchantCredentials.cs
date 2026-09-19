namespace Merchant.Api.Domains.Merchants.Features.Queries;

// 045 US3 / kontrat #3: store credential-giriş ekranının kayıt-anı doğrulaması (merchant.read).
// Yalnız "ikili eşleşiyor VE merchant Active" bilgisini döner; başka hiçbir alan sızmaz.
// İkili ve sonuç LOGLANMAZ (key yüksek entropili sır).
public static class ValidateMerchantCredentials
{
    public record ValidateMerchantCredentialsQuery(Guid MerchantId, string MerchantKey);

    public class ValidateMerchantCredentialsResponse
    {
        public bool Valid { get; set; }
    }

    public class ValidateMerchantCredentialsQueryHandler
    {
        public async Task<FeatureObjectResultModel<ValidateMerchantCredentialsResponse>> Handle(
            ValidateMerchantCredentialsQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(query.MerchantId, ct);

            var valid = merchant is not null
                        && merchant.Status == MerchantStatus.Active
                        && !string.IsNullOrWhiteSpace(query.MerchantKey)
                        && string.Equals(merchant.MerchantKey, query.MerchantKey.Trim(), StringComparison.Ordinal);

            return FeatureObjectResultModel<ValidateMerchantCredentialsResponse>.Ok(
                new ValidateMerchantCredentialsResponse { Valid = valid });
        }
    }

    public static RouteGroupBuilder ValidateMerchantCredentialsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/credentials/validate",
                async ([FromBody] ValidateMerchantCredentialsQuery query, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<ValidateMerchantCredentialsResponse>>(query);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("ValidateMerchantCredentials")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantRead)
            .Produces<ValidateMerchantCredentialsResponse>();

        return group;
    }
}
