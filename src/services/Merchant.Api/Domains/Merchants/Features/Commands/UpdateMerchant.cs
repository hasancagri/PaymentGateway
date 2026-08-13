namespace Merchant.Api.Domains.Merchants.Features.Commands;

// 023: admin düzleminden merchant güncelleme — Create ile aynı doğrulama seti (aggregate'te).
// Id / MerchantKey / Status / SubMerchantKey bu uçtan değişmez; event yayını yok.
public static class UpdateMerchant
{
    public record UpdateMerchantCommand(
        Guid MerchantId,
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

    public class UpdateMerchantResponse
    {
        public Guid MerchantId { get; set; }
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
    }

    [Transactional]
    public class UpdateMerchantCommandHandler
    {
        public async Task<FeatureObjectResultModel<UpdateMerchantResponse>> Handle(
            UpdateMerchantCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            if (!Enum.TryParse<MerchantType>(cmd.Type?.Trim(), ignoreCase: true, out var type))
                return FeatureObjectResultModel<UpdateMerchantResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Type),
                    Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
                });

            var merchant = await session.LoadAsync<Merchant>(cmd.MerchantId, ct);
            if (merchant is null || merchant.IsDeleted)
                return FeatureObjectResultModel<UpdateMerchantResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.MerchantId),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = merchant.UpdateDetails(
                type, cmd.Name, cmd.Email, cmd.GsmNumber, cmd.Address, cmd.Iban,
                cmd.ContactName, cmd.ContactSurname,
                cmd.IdentityNumber, cmd.TaxOffice, cmd.TaxNumber, cmd.LegalCompanyTitle);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<UpdateMerchantResponse>.Error(result.Messages);

            session.Store(merchant);

            return FeatureObjectResultModel<UpdateMerchantResponse>.Ok(new UpdateMerchantResponse
            {
                MerchantId = merchant.Id,
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
                SubMerchantKey = merchant.SubMerchantKey
            });
        }
    }
}

public static class UpdateMerchantEndpoint
{
    /// <summary>Gövde modeli; <c>merchantId</c> rotadan gelir.</summary>
    public record UpdateMerchantBody(
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

    public static RouteGroupBuilder UpdateMerchantGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/{merchantId:guid}",
                async (Guid merchantId, [FromBody] UpdateMerchantBody body, IMessageBus bus) =>
                {
                    var cmd = new UpdateMerchant.UpdateMerchantCommand(
                        merchantId, body.Type, body.Name, body.Email, body.GsmNumber, body.Address,
                        body.Iban, body.ContactName, body.ContactSurname,
                        body.IdentityNumber, body.TaxOffice, body.TaxNumber, body.LegalCompanyTitle);
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<UpdateMerchant.UpdateMerchantResponse>>(cmd);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("UpdateMerchant")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantWrite, AuthorizationPolicies.AdminPlaneOnly)
            .Produces<UpdateMerchant.UpdateMerchantResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}
