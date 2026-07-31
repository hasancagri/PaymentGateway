using Merchant.Api.Domains.Merchants.Lookups;

namespace Merchant.Api.Domains.Merchants.Features.Commands;

public static class CreateMerchant
{
    public record CreateMerchantCommand(
        string Name,
        string Email,
        string Phone,
        string CountryCode,
        string CityCode,
        string Mcc,
        string WebhookUrl);

    public class CreateMerchantResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateMerchantCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateMerchantResponse>> Handle(
            CreateMerchantCommand cmd,
            IDocumentSession session,
            ICountryLookup countryLookup,
            ICityLookup cityLookup,
            IMccLookup mccLookup,
            CancellationToken ct)
        {
            // 1) Saf format doğrulaması aggregate'te.
            var result = Merchant.Create(cmd.Name, cmd.Email, cmd.Phone, cmd.CountryCode,
                cmd.CityCode, cmd.Mcc, cmd.WebhookUrl);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<CreateMerchantResponse>.Error(result.Messages);

            // 2) Varlık doğrulaması handler'da (referans veri okuması).
            var errors = new List<MessageItem>();
            if (!countryLookup.Exists(cmd.CountryCode))
                errors.Add(NotFound(nameof(cmd.CountryCode)));

            if (!cityLookup.Exists(cmd.CityCode))
                errors.Add(NotFound(nameof(cmd.CityCode)));
            else if (!cityLookup.BelongsTo(cmd.CityCode, cmd.CountryCode))
                errors.Add(new MessageItem
                {
                    Property = nameof(cmd.CityCode),
                    Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
                });

            if (!mccLookup.Exists(cmd.Mcc))
                errors.Add(NotFound(nameof(cmd.Mcc)));

            if (errors.Count > 0)
                return FeatureObjectResultModel<CreateMerchantResponse>.Error(errors);

            session.Store(result.Data!);

            return FeatureObjectResultModel<CreateMerchantResponse>.Ok(new CreateMerchantResponse
            {
                Id = result.Data!.Id
            });
        }

        private static MessageItem NotFound(string property) => new()
        {
            Property = property,
            Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
        };
    }
}

public static class CreateMerchantCommandEndpoint
{
    public static RouteGroupBuilder CreateMerchantGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/",
                async ([FromBody] CreateMerchant.CreateMerchantCommand cmd, IMessageBus bus) =>
                {
                    var result =
                        await bus.InvokeAsync<FeatureObjectResultModel<CreateMerchant.CreateMerchantResponse>>(cmd);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("CreateMerchant")
            .MapToApiVersion(1, 0)
            .Produces<CreateMerchant.CreateMerchantResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}