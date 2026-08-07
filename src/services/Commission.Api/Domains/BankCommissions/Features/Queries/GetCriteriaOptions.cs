namespace Commission.Api.Domains.BankCommissions.Features.Queries;

/// <summary>
/// Grid ekseni + tek-tek giriş dropdown'ları için kriter enum değerleri. Tek kaynak: domain
/// enum'ları (CardBrand/CardType/TransactionRegion). UI bunları kopyalamaz — buradan çeker.
/// </summary>
public static class GetCriteriaOptions
{
    public record GetCriteriaOptionsQuery();

    public class GetCriteriaOptionsResponse
    {
        public List<string> CardBrands { get; set; } = new();
        public List<string> CardTypes { get; set; } = new();
        public List<string> TransactionRegions { get; set; } = new();
    }

    public class GetCriteriaOptionsQueryHandler
    {
        public Task<FeatureObjectResultModel<GetCriteriaOptionsResponse>> Handle(GetCriteriaOptionsQuery query)
        {
            var response = new GetCriteriaOptionsResponse
            {
                CardBrands = Enum.GetNames<CardBrand>().ToList(),
                CardTypes = Enum.GetNames<CardType>().ToList(),
                TransactionRegions = Enum.GetNames<TransactionRegion>().ToList()
            };

            return Task.FromResult(FeatureObjectResultModel<GetCriteriaOptionsResponse>.Ok(response));
        }
    }
}

public static class GetCriteriaOptionsQueryEndpoint
{
    public static RouteGroupBuilder GetCriteriaOptionsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/criteria-options",
                async (IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<GetCriteriaOptions.GetCriteriaOptionsResponse>>(
                        new GetCriteriaOptions.GetCriteriaOptionsQuery());
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("GetCriteriaOptions")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.CommissionRead)
            .Produces<GetCriteriaOptions.GetCriteriaOptionsResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}