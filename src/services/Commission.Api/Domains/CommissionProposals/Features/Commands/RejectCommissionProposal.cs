namespace Commission.Api.Domains.CommissionProposals.Features.Commands;

/// <summary>
/// US3 — ret icrası: bilet → proposal bul, gerekçeyle <c>Reject</c> (gerekçe zorunlu — FR-006).
/// Gerekçe admin'e agent sorgusunda (commission_proposal_status) ve admin ekranında görünür.
/// Bilet kuralları Accept ile aynı; geçersiz bilette durum değişmez.
/// </summary>
public static class RejectCommissionProposal
{
    public record RejectCommissionProposalCommand(string Ticket, string Reason);

    public class RejectCommissionProposalResponse
    {
        public Guid MerchantId { get; set; }
    }

    [Transactional]
    public class RejectCommissionProposalCommandHandler
    {
        public async Task<FeatureObjectResultModel<RejectCommissionProposalResponse>> Handle(
            RejectCommissionProposalCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var proposal = await session.Query<CommissionProposal>()
                .Where(p => p.DecisionTicket == cmd.Ticket && !p.IsDeleted)
                .FirstOrDefaultAsync(ct);
            if (proposal is null)
            {
                return FeatureObjectResultModel<RejectCommissionProposalResponse>.Error(new MessageItem
                {
                    Property = "Ticket",
                    Code = CommissionResourceConstants.PROPOSAL_TICKET_INVALID
                });
            }

            var reject = proposal.Reject(cmd.Reason, DateTime.UtcNow);
            if (!reject.IsSuccess)
                return FeatureObjectResultModel<RejectCommissionProposalResponse>.Error(reject.Messages);
            session.Update(proposal);

            return FeatureObjectResultModel<RejectCommissionProposalResponse>.Ok(new RejectCommissionProposalResponse
            {
                MerchantId = proposal.MerchantId
            });
        }
    }
}