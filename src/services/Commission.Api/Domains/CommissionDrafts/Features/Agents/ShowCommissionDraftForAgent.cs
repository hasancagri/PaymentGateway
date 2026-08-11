namespace Commission.Api.Domains.CommissionDrafts.Features.Agents;

/// <summary>
/// US5 (agent yüzeyi) — taslağın güncel tam tablosunu (satır no'lu) + kilit durumunu döner; uzun
/// düzenlemenin son kontrolü "gönder"den önce buradan yapılır. Read-only; agent slice'ları
/// Commands/Queries'e gitmez, okumayı kendi içinde yapar (015).
/// </summary>
public static class ShowCommissionDraftForAgent
{
    public record ShowCommissionDraftQuery(Guid MerchantId);

    public class ShowCommissionDraftResponse
    {
        public List<DraftRowItem> Rows { get; set; } = new();
        public bool IsLocked { get; set; }
    }

    public class DraftRowItem
    {
        public int RowNo { get; set; }
        public string Bank { get; set; } = string.Empty;
        public string CardBrand { get; set; } = string.Empty;
        public string CardType { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public int Installment { get; set; }
        public decimal Rate { get; set; }
    }

    public class ShowCommissionDraftForAgentQueryHandler
    {
        public async Task<FeatureObjectResultModel<ShowCommissionDraftResponse>> Handle(
            ShowCommissionDraftQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var draft = await session.LoadAsync<CommissionDraft>(query.MerchantId, ct);
            if (draft is null)
                return FeatureObjectResultModel<ShowCommissionDraftResponse>.NotFound();

            return FeatureObjectResultModel<ShowCommissionDraftResponse>.Ok(new ShowCommissionDraftResponse
            {
                IsLocked = draft.IsLocked,
                Rows = draft.Rows.Select(r => new DraftRowItem
                {
                    RowNo = r.RowNo,
                    Bank = r.BankName,
                    CardBrand = r.Criteria.CardBrand.ToString(),
                    CardType = r.Criteria.CardType.ToString(),
                    Region = r.Criteria.TransactionRegion.ToString(),
                    Installment = r.Criteria.InstallmentCount,
                    Rate = r.Rate
                }).ToList()
            });
        }
    }
}
