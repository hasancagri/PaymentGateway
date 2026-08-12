namespace Commission.Api.Domains.CommissionProposals.Features.Agents;

/// <summary>
/// US5 (agent yüzeyi) — merchant'ın SON teklifinin durumu: None / Pending / Accepted / Rejected
/// (+ ret gerekçesi + karar zamanı). Ret gerekçesi görünmeden revizyon döngüsü işlemez (FR-006).
/// Superseded ara durumu dışarı "None gibi" sızmaz — son kayıt neyse o döner; Superseded en son
/// kayıtsa yeni tur zaten açılmıştır (pratikte son kayıt Pending olur). Read-only (015).
/// </summary>
public static class CommissionProposalStatusForAgent
{
    public record CommissionProposalStatusQuery(Guid MerchantId);

    public class CommissionProposalStatusResponse
    {
        public string Status { get; set; } = "None";
        public Guid? ProposalId { get; set; }
        public DateTime? DecidedTime { get; set; }
        public string? RejectReason { get; set; }
    }

    public class CommissionProposalStatusForAgentQueryHandler
    {
        public async Task<FeatureObjectResultModel<CommissionProposalStatusResponse>> Handle(
            CommissionProposalStatusQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var latest = await session.Query<CommissionProposal>()
                .Where(p => p.MerchantId == query.MerchantId && !p.IsDeleted)
                .OrderByDescending(p => p.CreatedTime)
                .FirstOrDefaultAsync(ct);

            if (latest is null)
                return FeatureObjectResultModel<CommissionProposalStatusResponse>.Ok(new CommissionProposalStatusResponse
                {
                    Status = "None"
                });

            return FeatureObjectResultModel<CommissionProposalStatusResponse>.Ok(new CommissionProposalStatusResponse
            {
                Status = latest.Status.ToString(),
                ProposalId = latest.Id,
                DecidedTime = latest.DecidedTime,
                RejectReason = latest.RejectReason
            });
        }
    }
}
