namespace Payment.Api.Domains.BinCards.Features.Commands;

/// <summary>
/// Yayınlanan BIN listesini idempotent toplu upsert eder (kimlik BinNumber). Geçersiz/eksik kayıt
/// atlanır + raporlanır; batch bozulmaz. <c>cardType/cardBrand/cardProgram</c> taşınabilir int kod
/// (CP.VPOS legend) — sınırda domain enum'una çevrilir.
/// </summary>
public static class ImportBinCards
{
    public record BinCardImportItem(
        string? BinNumber,
        string? BankCode,
        int CardType,
        int CardBrand,
        bool Commercial,
        int CardProgram);

    public record ImportBinCardsCommand(List<BinCardImportItem> Items);

    /// <summary>Saf geçerlilik: kimlik (BinNumber) ve BankCode zorunlu; aksi halde kayıt atlanır.</summary>
    public static bool IsValid(BinCardImportItem item) =>
        !string.IsNullOrWhiteSpace(item.BinNumber) && !string.IsNullOrWhiteSpace(item.BankCode);

    public class ImportBinCardsResponse
    {
        public int ImportedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> SkippedReasons { get; set; } = new();
    }

    [Transactional]
    public class ImportBinCardsCommandHandler
    {
        public async Task<FeatureObjectResultModel<ImportBinCardsResponse>> Handle(
            ImportBinCardsCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var response = new ImportBinCardsResponse();
            var items = cmd.Items ?? new List<BinCardImportItem>();

            // Yeni/güncelleme ayrımı için gelen kimliklerden hâlihazırda var olanları çek.
            var incomingBins = items
                .Where(i => !string.IsNullOrWhiteSpace(i.BinNumber))
                .Select(i => i.BinNumber!)
                .Distinct()
                .ToList();

            var existing = incomingBins.Count == 0
                ? new HashSet<string>()
                : (await session.Query<BinCard>()
                        .Where(x => incomingBins.Contains(x.BinNumber))
                        .Select(x => x.BinNumber)
                        .ToListAsync(ct))
                    .ToHashSet();

            var invalidCount = 0;
            var storedInBatch = new HashSet<string>();

            foreach (var item in items)
            {
                if (!IsValid(item))
                {
                    invalidCount++;
                    response.SkippedCount++;
                    continue;
                }

                var card = BinCardMapping.FromCodes(
                    item.BinNumber!, item.BankCode, item.CardType, item.CardBrand, item.CardProgram, item.Commercial);

                session.Store(card); // upsert — kimlik BinNumber (idempotent)

                if (existing.Contains(card.BinNumber) || !storedInBatch.Add(card.BinNumber))
                    response.UpdatedCount++;
                else
                    response.ImportedCount++;
            }

            if (invalidCount > 0)
                response.SkippedReasons.Add($"binNumber/bankCode eksik: {invalidCount} kayıt");

            return FeatureObjectResultModel<ImportBinCardsResponse>.Ok(response);
        }
    }
}