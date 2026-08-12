using Commission.Api.Domains.CommissionDrafts;

namespace Commission.Api.Domains.CommissionProposals.Features.Commands;

/// <summary>
/// US2 — kabul icrası (insansız zincir, SC-002). Bilet → proposal bul; <c>Accept</c> (tek kullanım +
/// TTL + yalnız Pending); draft <c>Lock</c>; taslak satırları <c>MerchantCommission</c>'a kopyala;
/// <c>MerchantCommissionGridReady</c> publish — hepsi tek <c>[Transactional]</c> (outbox). Mevcut
/// <c>MerchantCommissionGridReadyHandler</c> zinciri (Merchant Active koşulu #2) DEĞİŞMEZ.
/// </summary>
public static class AcceptCommissionProposal
{
    public record AcceptCommissionProposalCommand(string Ticket);

    public class AcceptCommissionProposalResponse
    {
        public Guid MerchantId { get; set; }
        public int RowCount { get; set; }
    }

    [Transactional]
    public class AcceptCommissionProposalCommandHandler
    {
        public async Task<FeatureObjectResultModel<AcceptCommissionProposalResponse>> Handle(
            AcceptCommissionProposalCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var proposal = await session.Query<CommissionProposal>()
                .Where(p => p.DecisionTicket == cmd.Ticket && !p.IsDeleted)
                .FirstOrDefaultAsync(ct);
            if (proposal is null)
            {
                return FeatureObjectResultModel<AcceptCommissionProposalResponse>.Error(new MessageItem
                {
                    Property = "Ticket",
                    Code = CommissionResourceConstants.PROPOSAL_TICKET_INVALID
                });
            }

            var accept = proposal.Accept(DateTime.UtcNow);
            if (!accept.IsSuccess)
                return FeatureObjectResultModel<AcceptCommissionProposalResponse>.Error(accept.Messages);
            session.Update(proposal);

            var draft = await session.LoadAsync<CommissionDraft>(proposal.MerchantId, ct);
            if (draft is null)
            {
                return FeatureObjectResultModel<AcceptCommissionProposalResponse>.Error(new MessageItem
                {
                    Property = "Draft",
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });
            }

            var @lock = draft.Lock();
            if (!@lock.IsSuccess)
                return FeatureObjectResultModel<AcceptCommissionProposalResponse>.Error(@lock.Messages);
            session.Update(draft);

            // Teklif satırları merchant komisyonuna kopyalanır (tek yazma yolu — FR-013 sonrası).
            // MerchantCommission banka-bağımsız (MerchantId + Criteria benzersiz); aynı kombinasyon
            // birden çok banka satırında varsa EN YÜKSEK oran alınır — merchant oranı tüm banka
            // tabanlarını karşılasın (taban bekçisiyle tutarlı, read-time tavan işaretini tetiklemez).
            var existing = await session.Query<MerchantCommission>()
                .Where(c => c.MerchantId == proposal.MerchantId && !c.IsDeleted)
                .ToListAsync(ct);
            var existingByCriteria = existing.ToDictionary(c => c.Criteria);

            var byCriteria = proposal.Rows
                .GroupBy(r => r.Criteria)
                .Select(g => (Criteria: g.Key, Rate: g.Max(r => r.Rate)));

            foreach (var (criteria, rate) in byCriteria)
            {
                if (existingByCriteria.TryGetValue(criteria, out var current))
                {
                    var update = current.UpdateRate(rate);
                    if (!update.IsSuccess)
                        return FeatureObjectResultModel<AcceptCommissionProposalResponse>.Error(update.Messages);
                    session.Update(current);
                    continue;
                }

                var create = MerchantCommission.Create(proposal.MerchantId, criteria, rate);
                if (!create.IsSuccess)
                    return FeatureObjectResultModel<AcceptCommissionProposalResponse>.Error(create.Messages);
                session.Store(create.Data!);
            }

            // Aktivasyon zinciri: mevcut kontrat, mevcut tüketici (Merchant.Api) — değişmez.
            await bus.PublishAsync(new Shared.IntegrationEvents.MerchantCommissionGridReady(proposal.MerchantId));

            return FeatureObjectResultModel<AcceptCommissionProposalResponse>.Ok(new AcceptCommissionProposalResponse
            {
                MerchantId = proposal.MerchantId,
                RowCount = proposal.Rows.Count
            });
        }
    }
}