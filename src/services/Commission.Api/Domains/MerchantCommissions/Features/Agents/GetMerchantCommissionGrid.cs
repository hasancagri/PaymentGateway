
namespace Commission.Api.Domains.MerchantCommissions.Features.Agents;

/// <summary>
/// US4 — komisyon Excel orkestrasyonu (D14) grid kaynağı. Ready grid'i LLM'in Excel'e çevirebileceği
/// düz tablo (satır/sütun) olarak döner. Ready değilse (Draft/tanımsız) → isEmpty:true, rows boş
/// (LLM "hazır değil" der; Excel üretilmez). Read-only.
/// </summary>
public static class GetMerchantCommissionGrid
{
    public record GetMerchantCommissionGridQuery(Guid MerchantId);

    public class GetMerchantCommissionGridResponse
    {
        public Guid MerchantId { get; set; }
        public string Status { get; set; } = "Draft";
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
            var grid = await session.LoadAsync<MerchantCommissionGrid>(query.MerchantId, ct);

            // Yalnız Ready grid Excel'e döker; aksi halde boş (isEmpty).
            if (grid is null || grid.Status != GridStatus.Ready)
                return FeatureObjectResultModel<GetMerchantCommissionGridResponse>.Ok(new GetMerchantCommissionGridResponse
                {
                    MerchantId = query.MerchantId,
                    Status = grid?.Status.ToString() ?? GridStatus.Draft.ToString(),
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
                Status = GridStatus.Ready.ToString(),
                IsEmpty = rows.Count == 0,
                Columns = GridColumns,
                Rows = rows
            });
        }
    }
}
