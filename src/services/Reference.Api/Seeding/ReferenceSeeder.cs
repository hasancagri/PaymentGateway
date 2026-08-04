using Marten.Schema;
using Reference.Api.Domains.Banks;
using Reference.Api.Domains.Cities;
using Reference.Api.Domains.Countries;
using Reference.Api.Domains.Mccs;

namespace Reference.Api.Seeding;

/// <summary>
/// İlk seed: gömülü JSON kaynaklarından katalog aggregate'lerini doldurur. İdempotent — yalnız
/// kodu DB'de olmayan kayıtları ekler (katalog büyütmeyi de destekler: yeni kayıt eklenmiş seed'le
/// restart → yalnız yeni kayıt insert). CP.VPOS'a bağımlılık YOK (embedded resource). Marten
/// <see cref="IInitialData"/> startup hook'u (Program.cs <c>InitializeWith</c>).
/// Olay yayını <see cref="ReferenceStartupPublisher"/>'da (bus erişimi orada).
/// </summary>
public class ReferenceSeeder : IInitialData
{
    public async Task Populate(IDocumentStore store, CancellationToken cancellation)
    {
        await using var session = store.LightweightSession();

        await SeedCountriesAsync(session, cancellation);
        await SeedCitiesAsync(session, cancellation);
        await SeedMccsAsync(session, cancellation);
        await SeedBanksAsync(session, cancellation);

        await session.SaveChangesAsync(cancellation);
    }

    private static async Task SeedCountriesAsync(IDocumentSession session, CancellationToken ct)
    {
        var existing = (await session.Query<Country>().Select(c => c.Code).ToListAsync(ct)).ToHashSet();
        foreach (var r in Load("countries.json"))
        {
            if (existing.Contains(r.Code)) continue;
            var result = Country.Create(r.Code, r.Name);
            if (result.IsSuccess) session.Store(result.Data!);
        }
    }

    private static async Task SeedCitiesAsync(IDocumentSession session, CancellationToken ct)
    {
        var existing = (await session.Query<City>().Select(c => c.Code).ToListAsync(ct)).ToHashSet();
        foreach (var r in Load("cities.json"))
        {
            if (existing.Contains(r.Code)) continue;
            var result = City.Create(r.Code, r.Name, r.CountryCode ?? string.Empty);
            if (result.IsSuccess) session.Store(result.Data!);
        }
    }

    private static async Task SeedMccsAsync(IDocumentSession session, CancellationToken ct)
    {
        var existing = (await session.Query<Mcc>().Select(c => c.Code).ToListAsync(ct)).ToHashSet();
        foreach (var r in Load("mccs.json"))
        {
            if (existing.Contains(r.Code)) continue;
            var result = Mcc.Create(r.Code, r.Name);
            if (result.IsSuccess) session.Store(result.Data!);
        }
    }

    private static async Task SeedBanksAsync(IDocumentSession session, CancellationToken ct)
    {
        var existing = (await session.Query<Bank>().Select(c => c.Code).ToListAsync(ct)).ToHashSet();
        foreach (var r in Load("banks.json"))
        {
            if (existing.Contains(r.Code)) continue;
            var result = Bank.Create(r.Code, r.Name);
            if (result.IsSuccess) session.Store(result.Data!);
        }
    }

    /// <summary>Gömülü JSON seed kaydını yükler (dosya adı suffix ile eşleşir).</summary>
    public static IReadOnlyList<SeedRecord> Load(string resourceSuffix)
    {
        var assembly = typeof(ReferenceSeeder).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith(resourceSuffix, StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        return JsonConvert.DeserializeObject<List<SeedRecord>>(json) ?? new List<SeedRecord>();
    }

    /// <summary>Gömülü JSON kaydı. CountryCode yalnız cities.json'da dolu.</summary>
    public sealed class SeedRecord
    {
        [JsonProperty("code")] public string Code { get; set; } = string.Empty;
        [JsonProperty("name")] public string Name { get; set; } = string.Empty;
        [JsonProperty("countryCode")] public string? CountryCode { get; set; }
    }
}