using Marten.Schema;

namespace Payment.Api.Domains.BinCards;

/// <summary>
/// İlk seed: katalog boşsa gömülü <c>bincards.json</c> kaynağından bir kez doldurur. Doluysa
/// hiçbir şey yapmaz (idempotent). Kaynak assembly'ye embedded resource olarak paketlenir —
/// CP.VPOS'a bağımlılık YOK. Marten <see cref="IInitialData"/> startup hook'u (Program.cs
/// <c>opts.InitialData.Add</c>).
/// </summary>
public class BinCardSeeder : IInitialData
{
    private const string ResourceSuffix = "bincards.json";

    public async Task Populate(IDocumentStore store, CancellationToken cancellation)
    {
        await using var session = store.LightweightSession();

        var alreadySeeded = await session.Query<BinCard>().AnyAsync(cancellation);
        if (alreadySeeded)
            return;

        var records = await LoadSeedRecordsAsync(cancellation);

        var cards = records
            .Where(r => !string.IsNullOrWhiteSpace(r.BinNumber))
            .Select(r => BinCardMapping.FromCodes(r.BinNumber!, r.BankCode, r.CardType, r.CardBrand, r.CardProgram, r.CommercialCard))
            .ToArray();

        session.Store(cards);
        await session.SaveChangesAsync(cancellation);
    }

    private static async Task<List<BinSeedRecord>> LoadSeedRecordsAsync(CancellationToken ct)
    {
        var assembly = typeof(BinCardSeeder).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith(ResourceSuffix, StringComparison.Ordinal));

        await using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(ct);

        return JsonConvert.DeserializeObject<List<BinSeedRecord>>(json) ?? new List<BinSeedRecord>();
    }

    /// <summary>Gömülü JSON kaydı (CP.VPOS legend int değerleri; sınırda domain enum'una çevrilir).</summary>
    private sealed class BinSeedRecord
    {
        [JsonProperty("binNumber")] public string? BinNumber { get; set; }
        [JsonProperty("bankCode")] public string? BankCode { get; set; }
        [JsonProperty("cardType")] public int CardType { get; set; }
        [JsonProperty("cardBrand")] public int CardBrand { get; set; }
        [JsonProperty("commercialCard")] public bool CommercialCard { get; set; }
        [JsonProperty("cardProgram")] public int CardProgram { get; set; }
    }
}