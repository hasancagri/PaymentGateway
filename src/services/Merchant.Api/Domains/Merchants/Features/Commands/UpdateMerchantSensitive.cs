namespace Merchant.Api.Domains.Merchants.Features.Commands;

// 044 US2: hassas alanların (Email, GsmNumber, IdentityNumber, Iban, TaxNumber) admin
// düzlemindeki TEK yazma yüzeyi — Admin UI hassas-veri sayfası (dar BFF) tüketir. Aggregate'in
// UpdateDetails'i tüm alanları tek imzada aldığından hassas-DIŞI alanlar MEVCUT değerlerden
// geçirilir (research R3); tip-koşullu kurallar (Individual→TCKN, Company→vergi) aynen çalışır.
public static class UpdateMerchantSensitive
{
    public record UpdateMerchantSensitiveCommand(
        Guid MerchantId,
        string Email,
        string GsmNumber,
        string? IdentityNumber,
        string Iban,
        string? TaxNumber);

    public class UpdateMerchantSensitiveResponse
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

    [Transactional]
    public class UpdateMerchantSensitiveCommandHandler
    {
        public async Task<FeatureObjectResultModel<UpdateMerchantSensitiveResponse>> Handle(
            UpdateMerchantSensitiveCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(cmd.MerchantId, ct);
            if (merchant is null || merchant.IsDeleted)
                return FeatureObjectResultModel<UpdateMerchantSensitiveResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.MerchantId),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = merchant.UpdateDetails(
                merchant.Type, merchant.Name, cmd.Email, cmd.GsmNumber, merchant.Address, cmd.Iban,
                merchant.ContactName, merchant.ContactSurname,
                cmd.IdentityNumber, merchant.TaxOffice, cmd.TaxNumber, merchant.LegalCompanyTitle);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<UpdateMerchantSensitiveResponse>.Error(result.Messages);

            session.Store(merchant);

            return FeatureObjectResultModel<UpdateMerchantSensitiveResponse>.Ok(new UpdateMerchantSensitiveResponse
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

public static class UpdateMerchantSensitiveEndpoint
{
    /// <summary>Gövde modeli; <c>merchantId</c> rotadan gelir.</summary>
    public record UpdateMerchantSensitiveBody(
        string Email,
        string GsmNumber,
        string? IdentityNumber,
        string Iban,
        string? TaxNumber);

    public static RouteGroupBuilder UpdateMerchantSensitiveGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/{merchantId:guid}/sensitive",
                async (Guid merchantId, [FromBody] UpdateMerchantSensitiveBody body, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<UpdateMerchantSensitive.UpdateMerchantSensitiveResponse>>(
                            new UpdateMerchantSensitive.UpdateMerchantSensitiveCommand(
                                merchantId, body.Email, body.GsmNumber, body.IdentityNumber,
                                body.Iban, body.TaxNumber));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("UpdateMerchantSensitive")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantWrite, AuthorizationPolicies.AdminPlaneOnly)
            .Produces<UpdateMerchantSensitive.UpdateMerchantSensitiveResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}