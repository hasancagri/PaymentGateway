namespace Commission.Api.Domains.CommissionProposals.Features.Queries;

/// <summary>
/// US2/US3 — karar sayfalarının (GET onay/gerekçe formu) bilet doğrulaması: bilet geçerli mi
/// (yalnız Pending + TTL dolmamış) + sayfa başlığı için satır sayısı. Karar İCRA ETMEZ (POST eder).
/// </summary>
public static class GetProposalByTicket
{
    public record GetProposalByTicketQuery(string Ticket);

    public class GetProposalByTicketResponse
    {
        public Guid MerchantId { get; set; }
        public int RowCount { get; set; }
        public bool IsDecidable { get; set; }
    }

    public class GetProposalByTicketQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetProposalByTicketResponse>> Handle(
            GetProposalByTicketQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var proposal = await session.Query<CommissionProposal>()
                .Where(p => p.DecisionTicket == query.Ticket && !p.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (proposal is null)
                return FeatureObjectResultModel<GetProposalByTicketResponse>.NotFound();

            return FeatureObjectResultModel<GetProposalByTicketResponse>.Ok(new GetProposalByTicketResponse
            {
                MerchantId = proposal.MerchantId,
                RowCount = proposal.Rows.Count,
                IsDecidable = proposal.Status == ProposalStatus.Pending && DateTime.UtcNow <= proposal.TicketExpiresAt
            });
        }
    }
}