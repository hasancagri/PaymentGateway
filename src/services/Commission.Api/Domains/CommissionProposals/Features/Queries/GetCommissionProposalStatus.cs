namespace Commission.Api.Domains.CommissionProposals.Features.Queries;

/// <summary>
/// US5 — Admin UI teklif durumu bölümü (salt gösterim): merchant'ın SON teklifinin durumu +
/// ret gerekçesi + karar zamanı. Agent karşılığı <c>CommissionProposalStatusForAgent</c> — bilinçli
/// tekrar (agent slice'ları Commands/Queries'e gitmez, tersi de kirletilmez; 015).
/// </summary>
public static class GetCommissionProposalStatus
{
    public record GetCommissionProposalStatusQuery(Guid MerchantId);

    public class GetCommissionProposalStatusResponse
    {
        public string Status { get; set; } = "None";
        public Guid? ProposalId { get; set; }
        public DateTime? DecidedTime { get; set; }
        public string? RejectReason { get; set; }
        public int RowCount { get; set; }
    }

    public class GetCommissionProposalStatusQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetCommissionProposalStatusResponse>> Handle(
            GetCommissionProposalStatusQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var latest = await session.Query<CommissionProposal>()
                .Where(p => p.MerchantId == query.MerchantId && !p.IsDeleted)
                .OrderByDescending(p => p.CreatedTime)
                .FirstOrDefaultAsync(ct);

            if (latest is null)
                return FeatureObjectResultModel<GetCommissionProposalStatusResponse>.Ok(new GetCommissionProposalStatusResponse
                {
                    Status = "None"
                });

            return FeatureObjectResultModel<GetCommissionProposalStatusResponse>.Ok(new GetCommissionProposalStatusResponse
            {
                Status = latest.Status.ToString(),
                ProposalId = latest.Id,
                DecidedTime = latest.DecidedTime,
                RejectReason = latest.RejectReason,
                RowCount = latest.Rows.Count
            });
        }
    }
}

public static class GetCommissionProposalStatusEndpoint
{
    public static RouteGroupBuilder GetCommissionProposalStatusGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/status",
                async ([FromQuery] Guid merchantId, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<GetCommissionProposalStatus.GetCommissionProposalStatusResponse>>(
                            new GetCommissionProposalStatus.GetCommissionProposalStatusQuery(merchantId));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("GetCommissionProposalStatus")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.CommissionRead)
            .Produces<GetCommissionProposalStatus.GetCommissionProposalStatusResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        return group;
    }
}
