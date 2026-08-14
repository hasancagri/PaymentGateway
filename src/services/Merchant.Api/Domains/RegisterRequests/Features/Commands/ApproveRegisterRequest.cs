namespace Merchant.Api.Domains.RegisterRequests.Features.Commands;

// 029 US2: onay — başvuru bilgileriyle Merchant.Create (Active doğar, MerchantKey üretir),
// MerchantCreated outbox'la Identity'ye gider (OpenIddict istemci senkronu — CreateMerchant
// slice'ındaki yayının bilinçli tekrarı, 015: slice'lar birbirini çağırmaz). Merchant.Create
// hata verirse (teorik — Submit aynı kurallarla doğruladı) başvuru Pending kalır.
public static class ApproveRegisterRequest
{
    public record ApproveRegisterRequestCommand(Guid RequestId);

    public class ApproveRegisterRequestResponse
    {
        public Guid RequestId { get; set; }
        public Guid MerchantId { get; set; }
    }

    [Transactional]
    public class ApproveRegisterRequestCommandHandler
    {
        public async Task<FeatureObjectResultModel<ApproveRegisterRequestResponse>> Handle(
            ApproveRegisterRequestCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var request = await session.LoadAsync<RegisterRequest>(cmd.RequestId, ct);
            if (request is null)
                return FeatureObjectResultModel<ApproveRegisterRequestResponse>.NotFound();

            var merchantResult = Domains.Merchants.Merchant.Create(
                request.Type, request.Name, request.Email, request.GsmNumber, request.Address,
                request.Iban, request.ContactName, request.ContactSurname,
                request.IdentityNumber, request.TaxOffice, request.TaxNumber, request.LegalCompanyTitle);
            if (!merchantResult.IsSuccess)
                return FeatureObjectResultModel<ApproveRegisterRequestResponse>.Error(merchantResult.Messages);

            var merchant = merchantResult.Data!;

            var approveResult = request.Approve(merchant.Id);
            if (!approveResult.IsSuccess)
                return FeatureObjectResultModel<ApproveRegisterRequestResponse>.Error(approveResult.Messages);

            session.Store(merchant);
            session.Store(request);

            // Identity.Server tüketir → OpenIddict istemci kaydı (client_secret = MerchantKey).
            // [Transactional] outbox: yayın yalnız DB commit'te gider.
            await bus.PublishAsync(new Shared.IntegrationEvents.MerchantCreated(
                merchant.Id, merchant.MerchantKey, merchant.Status.ToString()));

            return FeatureObjectResultModel<ApproveRegisterRequestResponse>.Ok(new ApproveRegisterRequestResponse
            {
                RequestId = request.Id,
                MerchantId = merchant.Id
            });
        }
    }
}

public static class ApproveRegisterRequestEndpoint
{
    public static RouteGroupBuilder ApproveRegisterRequestGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{requestId:guid}/approve",
                async (Guid requestId, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<ApproveRegisterRequest.ApproveRegisterRequestResponse>>(
                            new ApproveRegisterRequest.ApproveRegisterRequestCommand(requestId));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("ApproveRegisterRequest")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantWrite, AuthorizationPolicies.AdminPlaneOnly)
            .Produces<ApproveRegisterRequest.ApproveRegisterRequestResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}
