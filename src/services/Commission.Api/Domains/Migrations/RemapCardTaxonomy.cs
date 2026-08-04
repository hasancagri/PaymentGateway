using Commission.Api.Domains.BankCommissions;
using Commission.Api.Domains.MerchantCommissions;

namespace Commission.Api.Domains.Migrations;

/// <summary>
/// Kanonik kart taksonomiye (SharedKernel) tek-geçiş, idempotent veri migration'ı. Commission grid'i
/// eski enum int'lerini (VISA=1, MASTERCARD=2, TROY=3, AMEX=4; CREDIT=1, DEBIT=2, PREPAID=3) tutuyordu;
/// kanonik set farklı (Visa=0..; Debit=0, Credit=1, Prepaid=2). Eşleme <b>kaynak int'e göre tam sözlük</b>
/// ile TEK geçişte yapılır — sıralı/in-place güncelleme yasak (aralıklar çakışır: 2→1 sonra 3→2 üst üste biner).
/// İdempotency değere değil <see cref="BankCommission.TaxonomyVersion"/> işaretine dayanır (çakışma tuzağı).
/// </summary>
public static class CardTaxonomyRemap
{
    /// <summary>Güncel şema sürümü. Yeni kayıtlar bununla doğar; migration yalnız daha eskisini remap eder.</summary>
    public const int CurrentVersion = 1;

    // Kaynak (eski) int → kanonik enum. Yalnız eski-şema (version 0) dokümanlarına uygulanır.
    private static readonly Dictionary<int, CardBrand> BrandByLegacyInt = new()
    {
        [1] = CardBrand.Visa,       // VISA(1)  → 0
        [2] = CardBrand.MasterCard, // MASTER(2)→ 1
        [3] = CardBrand.Troy,       // TROY(3)  → 2
        [4] = CardBrand.Amex        // AMEX(4)  → 3
    };

    private static readonly Dictionary<int, CardType> TypeByLegacyInt = new()
    {
        [1] = CardType.Credit,  // CREDIT(1) → 1 (aynı)
        [2] = CardType.Debit,   // DEBIT(2)  → 0
        [3] = CardType.Prepaid  // PREPAID(3)→ 2
    };

    /// <summary>Saf: eski Criteria'yı kanonik Criteria'ya çevirir (kaynak int tam eşleme; region/taksit korunur).</summary>
    public static Criteria Remap(Criteria legacy)
    {
        var brand = BrandByLegacyInt.TryGetValue((int)legacy.CardBrand, out var b) ? b : legacy.CardBrand;
        var type = TypeByLegacyInt.TryGetValue((int)legacy.CardType, out var t) ? t : legacy.CardType;

        return Criteria.Create(brand, type, legacy.TransactionRegion, legacy.InstallmentCount).Data!;
    }
}

/// <summary>
/// Açılışta bir kez çalışır: eski-şema (TaxonomyVersion &lt; güncel) grid dokümanlarını remap eder,
/// işaretler, kaydeder. İkinci çalıştırmada işaretli dokümanlar atlanır (idempotent).
/// </summary>
public class RemapCardTaxonomyMigration : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<RemapCardTaxonomyMigration> _logger;

    public RemapCardTaxonomyMigration(IServiceProvider services, ILogger<RemapCardTaxonomyMigration> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();

        // Eksik alan (eski doküman) deserialize'da 0'a düşer → JSON sorgusu yerine bellekte filtrele.
        var banks = await session.Query<BankCommission>().ToListAsync(stoppingToken);
        var migratedBanks = 0;
        foreach (var doc in banks.Where(x => x.TaxonomyVersion < CardTaxonomyRemap.CurrentVersion))
        {
            doc.MigrateTaxonomy(CardTaxonomyRemap.Remap(doc.Criteria));
            session.Store(doc);
            migratedBanks++;
        }

        var merchants = await session.Query<MerchantCommission>().ToListAsync(stoppingToken);
        var migratedMerchants = 0;
        foreach (var doc in merchants.Where(x => x.TaxonomyVersion < CardTaxonomyRemap.CurrentVersion))
        {
            doc.MigrateTaxonomy(CardTaxonomyRemap.Remap(doc.Criteria));
            session.Store(doc);
            migratedMerchants++;
        }

        if (migratedBanks > 0 || migratedMerchants > 0)
            await session.SaveChangesAsync(stoppingToken);

        _logger.LogInformation(
            "CardTaxonomy remap: {Banks} BankCommission, {Merchants} MerchantCommission migrate edildi.",
            migratedBanks, migratedMerchants);
    }
}