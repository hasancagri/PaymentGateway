namespace Commission.Api.Domains.MerchantCommissions.Features.Agents;

/// <summary>
/// 013 US4 — komisyon Excel orkestrasyonu (D14) grid kaynağı. 019 (FR-013): GridStatus/Finalize
/// SÖKÜLDÜ — "hazır" olmanın tek kaynağı KABUL edilmiş teklif (Accepted CommissionProposal). Kabul
/// yoksa isEmpty:true, rows boş (Excel üretilmez). Read-only.
/// </summary>
public static class GetMerchantCommissionGrid
{
    public record GetMerchantCommissionGridQuery(Guid MerchantId);

    public class GetMerchantCommissionGridResponse
    {
        public Guid MerchantId { get; set; }

        /// <summary>Son teklifin durumu (None/Pending/Accepted/Rejected/Superseded) — bilgi amaçlı.</summary>
        public string Status { get; set; } = "None";

        public bool IsEmpty { get; set; } = true;
        public List<string> Columns { get; set; } = new();
        public List<List<string>> Rows { get; set; } = new();
    }

    public class GetMerchantCommissionGridQueryHandler
    {
        private static readonly List<string> GridColumns =
            new() { "Kart Markası", "Kart Tipi", "Bölge", "Taksit", "Oran (%)" };

        public async Task<FeatureObjectResultModel<GetMerchantCommissionGridResponse>> Handle(
            GetMerchantCommissionGridQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var latestProposal = await session.Query<CommissionProposal>()
                .Where(p => p.MerchantId == query.MerchantId && !p.IsDeleted)
                .OrderByDescending(p => p.CreatedTime)
                .FirstOrDefaultAsync(ct);

            var hasAccepted = await session.Query<CommissionProposal>()
                .Where(p => p.MerchantId == query.MerchantId && p.Status == ProposalStatus.Accepted && !p.IsDeleted)
                .AnyAsync(ct);

            // Yalnız KABUL edilmiş komisyon Excel'e döker; aksi halde boş (isEmpty).
            if (!hasAccepted)
                return FeatureObjectResultModel<GetMerchantCommissionGridResponse>.Ok(new GetMerchantCommissionGridResponse
                {
                    MerchantId = query.MerchantId,
                    Status = latestProposal?.Status.ToString() ?? "None",
                    IsEmpty = true,
                    Columns = GridColumns,
                    Rows = new List<List<string>>()
                });

            var merchantCommissions = await session.Query<MerchantCommission>()
                .Where(c => c.MerchantId == query.MerchantId && !c.IsDeleted)
                .ToListAsync(ct);

            var rows = merchantCommissions
                .OrderBy(c => c.Criteria.CardBrand.ToString())
                .ThenBy(c => c.Criteria.CardType.ToString())
                .ThenBy(c => c.Criteria.TransactionRegion.ToString())
                .ThenBy(c => c.Criteria.InstallmentCount)
                .Select(c => new List<string>
                {
                    c.Criteria.CardBrand.ToString(),
                    c.Criteria.CardType.ToString(),
                    c.Criteria.TransactionRegion.ToString(),
                    c.Criteria.InstallmentCount.ToString(),
                    c.Rate.ToString("0.00")
                })
                .ToList();

            return FeatureObjectResultModel<GetMerchantCommissionGridResponse>.Ok(new GetMerchantCommissionGridResponse
            {
                MerchantId = query.MerchantId,
                Status = ProposalStatus.Accepted.ToString(),
                IsEmpty = rows.Count == 0,
                Columns = GridColumns,
                Rows = rows
            });
        }
    }
}
