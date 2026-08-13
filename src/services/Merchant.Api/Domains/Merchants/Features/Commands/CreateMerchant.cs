namespace Merchant.Api.Domains.Merchants.Features.Commands;

// 023: admin düzleminden merchant kaydı — doğrulamalar aggregate'te (tip-uyum + IBAN + e-posta).
// MerchantKey YALNIZ bu yanıtta döner (SC-004); sonraki hiçbir sorgu yanıtında görünmez.
public static class CreateMerchant
{
    public record CreateMerchantCommand(
        string Type,
        string Name,
        string Email,
        string GsmNumber,
        string Address,
        string Iban,
        string ContactName,
        string ContactSurname,
        string? IdentityNumber,
        string? TaxOffice,
        string? TaxNumber,
        string? LegalCompanyTitle);

    public class CreateMerchantResponse
    {
        public Guid MerchantId { get; set; }

        /// <summary>Makine erişim sırrı — yalnız burada, bir kez (SC-004).</summary>
        public string MerchantKey { get; set; } = string.Empty;
    }

    [Transactional]
    public class CreateMerchantCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateMerchantResponse>> Handle(
            CreateMerchantCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            if (!Enum.TryParse<MerchantType>(cmd.Type?.Trim(), ignoreCase: true, out var type))
                return FeatureObjectResultModel<CreateMerchantResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Type),
                    Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
                });

            var result = Merchant.Create(
                type, cmd.Name, cmd.Email, cmd.GsmNumber, cmd.Address, cmd.Iban,
                cmd.ContactName, cmd.ContactSurname,
                cmd.IdentityNumber, cmd.TaxOffice, cmd.TaxNumber, cmd.LegalCompanyTitle);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<CreateMerchantResponse>.Error(result.Messages);

            var merchant = result.Data!;
            session.Store(merchant);

            // Identity.Server tüketir → OpenIddict istemci kaydı (client_secret = MerchantKey).
            // [Transactional] outbox: yayın yalnız DB commit'te gider (FR-005 atomiklik).
            await bus.PublishAsync(new Shared.IntegrationEvents.MerchantCreated(
                merchant.Id, merchant.MerchantKey, merchant.Status.ToString()));

            return FeatureObjectResultModel<CreateMerchantResponse>.Ok(new CreateMerchantResponse
            {
                MerchantId = merchant.Id,
                MerchantKey = merchant.MerchantKey
            });
        }
    }
}

public static class CreateMerchantEndpoint
{
    public static RouteGroupBuilder CreateMerchantGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/",
                async ([FromBody] CreateMerchant.CreateMerchantCommand cmd, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<CreateMerchant.CreateMerchantResponse>>(cmd);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("CreateMerchant")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantWrite, AuthorizationPolicies.AdminPlaneOnly)
            .Produces<CreateMerchant.CreateMerchantResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}
