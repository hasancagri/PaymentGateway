using System.Reflection;

namespace Payment.Api.Domains.BinCards;

/// <summary>
/// 090 (FR-018): BinCard referans verisini startup'ta embedded JSON'dan yükler (statik seed; güncelleme =
/// reseed, dev). Her kayıt Bin (kimlik) ile upsert edilir → idempotent (yeniden başlatma güvenli). Canlı
/// BIN besleme kaynağı sonraki versiyon. Dev-faz: defansif migration yok (feedback_dev_phase_no_defensive_migrations).
/// </summary>
public static class BinCardSeeder
{
    private const string ResourceSuffix = "Domains.BinCards.bin-seed.json";

    public static async Task SeedAsync(IDocumentSession session, CancellationToken ct = default)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(ResourceSuffix, StringComparison.Ordinal));
        if (resourceName is null) return;

        await using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null) return;

        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(ct);
        var cards = JsonConvert.DeserializeObject<List<BinCard>>(json);
        if (cards is null || cards.Count == 0) return;

        foreach (var card in cards)
            session.Store(card);

        await session.SaveChangesAsync(ct);
    }
}
