namespace Merchant.Api.Domains.RegisterRequests.Features.Commands;

// 029 US2: red — neden zorunlu, kayıtta saklanır; durum sorgusunda karşı tarafa iletilir.
// Rejected terminaldir ama aynı e-posta yeniden başvurabilir (FR-003).
public static class RejectRegisterRequest
{
    public record RejectRegisterRequestCommand(Guid RequestId, string Reason);

    public class RejectRegisterRequestResponse
    {
        public Guid RequestId { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    [Transactional]
    public class RejectRegisterRequestCommandHandler
    {
        public async Task<FeatureObjectResultModel<RejectRegisterRequestResponse>> Handle(
            RejectRegisterRequestCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var request = await session.LoadAsync<RegisterRequest>(cmd.RequestId, ct);
            if (request is null)
                return FeatureObjectResultModel<RejectRegisterRequestResponse>.NotFound();

            var rejectResult = request.Reject(cmd.Reason);
            if (!rejectResult.IsSuccess)
                return FeatureObjectResultModel<RejectRegisterRequestResponse>.Error(rejectResult.Messages);

            session.Store(request);

            return FeatureObjectResultModel<RejectRegisterRequestResponse>.Ok(new RejectRegisterRequestResponse
            {
                RequestId = request.Id,
                Status = request.Status.ToString()
            });
        }
    }
}

public static class RejectRegisterRequestEndpoint
{
    public record RejectRegisterRequestBody(string Reason);

    public static RouteGroupBuilder RejectRegisterRequestGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{requestId:guid}/reject",
                async (Guid requestId, [FromBody] RejectRegisterRequestBody body, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<RejectRegisterRequest.RejectRegisterRequestResponse>>(
                            new RejectRegisterRequest.RejectRegisterRequestCommand(requestId, body.Reason));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("RejectRegisterRequest")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantWrite, AuthorizationPolicies.AdminPlaneOnly)
            .Produces<RejectRegisterRequest.RejectRegisterRequestResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}
