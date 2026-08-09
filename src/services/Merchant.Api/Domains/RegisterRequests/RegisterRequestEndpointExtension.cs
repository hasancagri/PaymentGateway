
namespace Merchant.Api.Domains.RegisterRequests;

// US2 — admin düzlemi RegisterRequest yönetimi. AdminPlaneOnly: merchant_id claim'li token giremez
// (merchant kendi talebini onaylayamaz). Admin BFF (admin-ui client, claim'siz) tüketir.
public static class RegisterRequestEndpointExtension
{
    public static void AddRegisterRequestGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/register-requests")
            .WithTags("register-requests")
            .WithApiVersionSet(apiVersionSet);

        group.MapGet("/",
                async (string? status, IMessageBus bus) =>
                {
                    RegisterRequestStatus? parsed = Enum.TryParse<RegisterRequestStatus>(status, true, out var s) ? s : null;
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<GetRegisterRequests.GetRegisterRequestsResponse>>(
                        new GetRegisterRequests.GetRegisterRequestsQuery(parsed));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("GetRegisterRequests")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantRead, AuthorizationPolicies.AdminPlaneOnly);

        group.MapGet("/{id:guid}",
                async (Guid id, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<GetRegisterRequest.GetRegisterRequestResponse>>(
                        new GetRegisterRequest.GetRegisterRequestQuery(id));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
                })
            .WithName("GetRegisterRequest")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantRead, AuthorizationPolicies.AdminPlaneOnly);

        group.MapPost("/{id:guid}/approve",
                async (Guid id, [FromBody] ReviewBody? body, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<ApproveRegisterRequest.ApproveRegisterRequestResponse>>(
                        new ApproveRegisterRequest.ApproveRegisterRequestCommand(id, body?.Note));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("ApproveRegisterRequest")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantWrite, AuthorizationPolicies.AdminPlaneOnly);

        group.MapPost("/{id:guid}/reject",
                async (Guid id, [FromBody] ReviewBody? body, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<RejectRegisterRequest.RejectRegisterRequestResponse>>(
                        new RejectRegisterRequest.RejectRegisterRequestCommand(id, body?.Note));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("RejectRegisterRequest")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantWrite, AuthorizationPolicies.AdminPlaneOnly);
    }

    public record ReviewBody(string? Note);
}
