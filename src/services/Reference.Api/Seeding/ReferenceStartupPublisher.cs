
namespace Reference.Api.Seeding;

/// <summary>
/// Açılışta katalog tam-setini <see cref="IntegrationEvents.ReferenceDataUpdated"/> ile yayınlar
/// (Kind başına bir mesaj). Reference.Api HTTP yüzeyi yok — tüketici (Merchant/Commission) veriyi
/// yalnız bu olayla alır; taze tüketici de durable queue üzerinden dolar (US4 bootstrap).
/// Tam-set gönderilir (küçük katalog); tüketici Code anahtarıyla idempotent upsert eder. Seed
/// değişince (yeni şehir vb.) restart'ta yeni kayıt bu tam-set içinde yayılır (US3, SC-006).
/// Publish-after-save: seeder DB'yi doldurduktan sonra okunup yayınlanır.
/// </summary>
public class ReferenceStartupPublisher : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ReferenceStartupPublisher> _logger;

    public ReferenceStartupPublisher(IServiceProvider services, ILogger<ReferenceStartupPublisher> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<IQuerySession>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var countries = await session.Query<Country>().ToListAsync(stoppingToken);
        var cities = await session.Query<City>().ToListAsync(stoppingToken);
        var mccs = await session.Query<Mcc>().ToListAsync(stoppingToken);
        var banks = await session.Query<Bank>().ToListAsync(stoppingToken);

        await Publish(bus, "Country", countries.Select(c => Item(c.Code, c.Name)));
        await Publish(bus, "City", cities.Select(c => Item(c.Code, c.Name, c.CountryCode)));
        await Publish(bus, "Mcc", mccs.Select(c => Item(c.Code, c.Name)));
        await Publish(bus, "Bank", banks.Select(c => Item(c.Code, c.Name)));

        _logger.LogInformation(
            "Reference full-set yayınlandı: {Countries} ülke, {Cities} şehir, {Mccs} MCC, {Banks} banka.",
            countries.Count, cities.Count, mccs.Count, banks.Count);
    }

    private static async Task Publish(IMessageBus bus, string kind, IEnumerable<IntegrationEvents.ReferenceItem> items)
    {
        var list = items.ToList();
        if (list.Count == 0)
            return;

        await bus.PublishAsync(new IntegrationEvents.ReferenceDataUpdated(kind, list));
    }

    private static IntegrationEvents.ReferenceItem Item(string code, string name, string? countryCode = null) =>
        new(code, name, countryCode);
}