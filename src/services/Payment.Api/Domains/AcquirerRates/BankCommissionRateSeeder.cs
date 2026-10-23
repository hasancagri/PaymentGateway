namespace Payment.Api.Domains.AcquirerRates;

/// <summary>
/// 092: BankCommissionRate read-model'ini startup'ta embedded JSON'dan yükler (BAŞLANGIÇ öngörüsü; BinCardSeeder
/// aynası). BankCode (kimlik) ile upsert → idempotent. GERÇEK besleme (banka onboarding/mutabakat → oran handoff)
/// sonraki dilim; bu seed o gelene dek öngörü sağlar. Dev-faz: defansif migration yok.
/// </summary>
public static class BankCommissionRateSeeder
{
    private const string ResourceSuffix = "Domains.AcquirerRates.bank-commission-seed.json";

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
        var rates = JsonConvert.DeserializeObject<List<BankCommissionRate>>(json);
        if (rates is null || rates.Count == 0) return;

        foreach (var rate in rates)
            session.Store(rate);

        await session.SaveChangesAsync(ct);
    }
}
