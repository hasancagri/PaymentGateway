using Commission.Api.Domains.CommissionDrafts.ValueObjects;

namespace Commission.Api.Domains.CommissionDrafts.Features.Agents;

/// <summary>
/// US3 (agent yüzeyi) — taslağı yapılandırılmış işlemlerle revize eder; hesap SUNUCUDA (FR-007),
/// dönen diff agent tarafından admin'e yankılanır (FR-008). Taban bekçisi güncel <c>BankCommission</c>
/// oranlarına bakar; ihlal BÜTÜN işlemi düşürür (FR-009). Merchant'a hiçbir şey gitmez (FR-010).
/// Değişmezlik bariyeri: Accepted teklif varsa RET (FR-012; IsLocked ile çifte bariyer).
/// Agent slice'ları Commands/Queries'e gitmez; okuma/yazma kendi içinde (015).
/// </summary>
public static class ReviseCommissionDraftForAgent
{
    /// <summary>MCP tool girdisi (contracts §1): LLM yalnız admin'in AÇIK değerlerini koyar.</summary>
    public record ReviseOperationInput(
        string Op, int? Row = null, string? Bank = null, int? Installment = null,
        ReviseFilterInput? Filter = null, decimal? Rate = null, decimal? Delta = null);

    public record ReviseFilterInput(string? Bank = null, int? Installment = null);

    public record ReviseCommissionDraftCommand(Guid MerchantId, List<ReviseOperationInput> Operations);

    public class ReviseCommissionDraftResponse
    {
        public List<DraftChangeItem> Changes { get; set; } = new();
    }

    public class DraftChangeItem
    {
        public int RowNo { get; set; }
        public string Bank { get; set; } = string.Empty;
        public int Installment { get; set; }
        public decimal OldRate { get; set; }
        public decimal NewRate { get; set; }
    }

    [Transactional]
    public class ReviseCommissionDraftForAgentCommandHandler
    {
        public async Task<FeatureObjectResultModel<ReviseCommissionDraftResponse>> Handle(
            ReviseCommissionDraftCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            // FR-012 değişmezlik bariyeri #1 (handler sorgusu; #2 = draft.IsLocked, Revise içinde).
            var hasAccepted = await session.Query<CommissionProposal>()
                .Where(p => p.MerchantId == cmd.MerchantId && p.Status == ProposalStatus.Accepted && !p.IsDeleted)
                .AnyAsync(ct);
            if (hasAccepted)
            {
                return FeatureObjectResultModel<ReviseCommissionDraftResponse>.Error(new MessageItem
                {
                    Property = "Proposal",
                    Code = CommissionResourceConstants.PROPOSAL_ALREADY_ACCEPTED
                });
            }

            var draft = await session.LoadAsync<CommissionDraft>(cmd.MerchantId, ct);
            if (draft is null)
                return FeatureObjectResultModel<ReviseCommissionDraftResponse>.NotFound();

            // Taban bekçisi girdisi: GÜNCEL banka oranları (teklif fotoğrafı değil — edge case:
            // banka grid'i teklif sonrası değiştiyse revize güncel tabana göre denetlenir).
            var bankCommissions = await session.Query<BankCommission>()
                .Where(c => !c.IsDeleted)
                .ToListAsync(ct);
            var floors = bankCommissions.ToDictionary(c => (c.BankCode, c.Criteria), c => c.Rate);

            var operations = cmd.Operations.Select(o => new DraftOperation
            {
                Op = o.Op,
                Row = o.Row,
                Bank = o.Bank,
                Installment = o.Installment,
                Filter = o.Filter is null ? null : new DraftOperationFilter
                {
                    Bank = o.Filter.Bank,
                    Installment = o.Filter.Installment
                },
                Rate = o.Rate,
                Delta = o.Delta
            }).ToList();

            var revise = draft.Revise(operations, floors);
            if (!revise.IsSuccess)
                return FeatureObjectResultModel<ReviseCommissionDraftResponse>.Error(revise.Messages);

            session.Update(draft);

            return FeatureObjectResultModel<ReviseCommissionDraftResponse>.Ok(new ReviseCommissionDraftResponse
            {
                Changes = revise.Data!.Select(c => new DraftChangeItem
                {
                    RowNo = c.RowNo,
                    Bank = c.BankName,
                    Installment = c.Installment,
                    OldRate = c.OldRate,
                    NewRate = c.NewRate
                }).ToList()
            });
        }
    }
}