
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddOpenApiDocumentation();

var paymentDb = builder.Configuration.GetConnectionString("paymentDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.PaymentSchemaName;
        opts.Connection(paymentDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s =>
            {
                s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
            });

        // 089: PaymentLink (pay-by-link). (MerchantId, OrderRef) AKTİF-tekil → PARTIAL unique (yalnız
        // Pending=0 / Opened=1); terminal (Expired/Cancelled) satır slot'u bırakır, re-create serbest (FR-003,
        // research D1). OpenToken tekil (gizli açılış ucu lookup + çakışma koruması, FR-007).
        // F4-DOĞRULA: Newtonsoft enum'u JSON'a int yazar → predicate (data->>'Status') in ('0','1'); canlı
        // DB'de üretilen index'i teyit et (yanlış biçim aktif-tekil + yarış korumasını sessizce kırar).
        opts.Schema.For<Payment.Api.Domains.PaymentLinks.PaymentLink>()
            .Index(x => new { x.MerchantId, x.OrderRef }, idx =>
            {
                idx.IsUnique = true;
                idx.Predicate = "(data ->> 'Status') in ('0', '1')";
            })
            .Index(x => x.OpenToken, idx => idx.IsUnique = true);

        // 090: BinCard = BC-içi salt-okur referans doküman (BIN tablosu). Kimlik = Bin (string). Startup'ta
        // BinCardSeeder embedded JSON'dan upsert eder (idempotent reseed).
        opts.Schema.For<Payment.Api.Domains.BinCards.BinCard>().Identity(x => x.Bin);
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    // Dev: tek dugum (Solo) - leader election/node-agent koordinasyonu kapali.
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    rabbit.DeclareExchange(RabbitMqConstants.PaymentCompleted.Exchange,
        e => { e.ExchangeType = ExchangeType.Fanout; });
    rabbit.DeclareExchange(RabbitMqConstants.PaymentFailed.Exchange,
        e => { e.ExchangeType = ExchangeType.Fanout; });

    opts.PublishMessage<Shared.IntegrationEvents.PaymentCompletedEvent>()
        .ToRabbitExchange(RabbitMqConstants.PaymentCompleted.Exchange);
    opts.PublishMessage<Shared.IntegrationEvents.PaymentFailedEvent>()
        .ToRabbitExchange(RabbitMqConstants.PaymentFailed.Exchange);
    // 088: PaymentChargedEvent publish'i + merchant.lifecycle tüketimi (statü referansı) SÖKÜLDÜ
    // (kart-vault charge + Merchant BC öldü; statü kapısı Phase-1'e devredildi).

    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    // 090: nested static webhook handler keşif güvencesi (ad-son-eki tabanlı tarama nested static'i
    // atlayabilir — CLAUDE.md tuzağı). StoreResultDelivery.Deliver → DeliverHandler.
    opts.Discovery.IncludeType(typeof(Payment.Api.Domains.PaymentLinks.Features.Commands.StoreResultDelivery.DeliverHandler));
    opts.Discovery.IncludeType(typeof(Payment.Api.Domains.PaymentLinks.Process.InDoubtReconciliation.CheckHandler));
    opts.Discovery.IncludeType(typeof(Payment.Api.Domains.PaymentLinks.Features.Commands.VoidPaymentLink.VoidPaymentLinkCommandHandler));
    opts.Discovery.IncludeType(typeof(Payment.Api.Domains.PaymentLinks.Features.Commands.RefundPaymentLink.RefundPaymentLinkCommandHandler));
    opts.Discovery.IncludeType(typeof(Payment.Api.Domains.PaymentLinks.Features.Queries.QueryPaymentStatus.QueryPaymentStatusHandler));
    opts.Discovery.IncludeType(typeof(Payment.Api.Domains.PaymentLinks.Features.Commands.Initiate3DPayment.Handler));
    opts.Discovery.IncludeType(typeof(Payment.Api.Domains.PaymentLinks.Features.Commands.Complete3DPayment.Handler));
    opts.Discovery.IncludeType(typeof(Payment.Api.Domains.PaymentLinks.Process.EndOfDayReconciliation.RunHandler));
});

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

// 011: JWT bearer (Identity.Server JWKS) + scope policy'leri; endpoint'ler policy'yi açıkça beyan eder.
// 088: cards.write + payment.charge (kart-vault/charge) SÖKÜLDÜ; X-Api-Key + MerchantApiKey/
// HostedPaymentApiKey merchant policy zinciri SÖKÜLDÜ (Merchant BC öldü). payment.* JWT scope kalır.
builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    AuthorizationScopes.PaymentRead,
    AuthorizationScopes.PaymentWrite);

builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// 088: eski ödeme sağlayıcısı transport Options'ları SÖKÜLDÜ (transport engine gitti).

// 089: PaymentLink ayarları (Ttl/GraceWindow/LinkBaseUrl/güven-sayfa metinleri — hepsi non-secret,
// appsettings). Options pattern (BindConfiguration + Validate); handler düz POCO inject eder.
builder.Services.AddOptions<Payment.Api.Options.PaymentLinkOptions>()
    .BindConfiguration(nameof(Payment.Api.Options.PaymentLinkOptions))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Payment.Api.Options.PaymentLinkOptions>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Payment.Api.Options.PaymentLinkOptions>>().Value);

// 090: NonSecure banka çekim Options (routing + acquirer öngörü + simüle bankalar + store webhook).
// Hepsi non-secret (appsettings) HARİÇ StoreCallbackOptions.CallbackSecret (secret-store, İlke VI).
// PaymentLinkOptions bloğunun aynası: BindConfiguration + Validate + düz POCO singleton-bridge (handler/
// router düz T inject eder, IOptions değil).
builder.Services.AddOptions<Payment.Api.Options.BankRoutingOptions>()
    .BindConfiguration(nameof(Payment.Api.Options.BankRoutingOptions))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Payment.Api.Options.BankRoutingOptions>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Payment.Api.Options.BankRoutingOptions>>().Value);

builder.Services.AddOptions<Payment.Api.Options.PosAccountOptions>()
    .BindConfiguration(nameof(Payment.Api.Options.PosAccountOptions))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Payment.Api.Options.PosAccountOptions>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Payment.Api.Options.PosAccountOptions>>().Value);

builder.Services.AddOptions<Payment.Api.Options.InterVposBankOptions>()
    .BindConfiguration(nameof(Payment.Api.Options.InterVposBankOptions))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Payment.Api.Options.InterVposBankOptions>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Payment.Api.Options.InterVposBankOptions>>().Value);

builder.Services.AddOptions<Payment.Api.Options.StoreCallbackOptions>()
    .BindConfiguration(nameof(Payment.Api.Options.StoreCallbackOptions))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Payment.Api.Options.StoreCallbackOptions>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Payment.Api.Options.StoreCallbackOptions>>().Value);

// Phase-1: banka çekim wiring. Her InterVposBankProfile'dan bir gerçek HTTP adapter (InterVposBankAdapter)
// TEK TEK IBankPaymentAdapter olarak kaydedilir → ctor IEnumerable<IBankPaymentAdapter> hepsini toplar
// (explicit IEnumerable reg DI'da çalışmaz). Profil listesi composition root'ta okunur (dinamik fan-out
// istisnası). Bugün tek canlı banka (MAVIBANK = simülatör); çok-banka routing/failover plumbing latent.
var interVposBanks = builder.Configuration
    .GetSection(nameof(Payment.Api.Options.InterVposBankOptions))
    .Get<Payment.Api.Options.InterVposBankOptions>() ?? new Payment.Api.Options.InterVposBankOptions();
foreach (var bank in interVposBanks.Banks)
{
    var profile = bank; // closure capture
    builder.Services.AddSingleton<Payment.Api.Domains.PaymentLinks.BankCharge.IBankPaymentAdapter>(
        _ => profile.Protocol == "TuruncuXml"
            ? new Payment.Api.Domains.PaymentLinks.BankCharge.TuruncuBankAdapter(profile)
            : new Payment.Api.Domains.PaymentLinks.BankCharge.InterVposBankAdapter(profile));
}
builder.Services.AddScoped<Payment.Api.Domains.PaymentLinks.BankCharge.BankChargeRouter>();

// 047: Payment.Api MCP ucu SÖKÜLDÜ (FR-010, "PG için tek MCP"). Payment tool taşımıyordu (hosted-CF
// ödeme store HTTP çağırır, agent değil); tool'suz /mcp bırakılmaz. payment.write REST policy'leri kalır.

var app = builder.Build();

// 090: BinCard referans verisini startup'ta seed et (embedded JSON → upsert, idempotent).
await using (var seedScope = app.Services.CreateAsyncScope())
{
    var seedSession = seedScope.ServiceProvider.GetRequiredService<IDocumentSession>();
    await Payment.Api.Domains.BinCards.BinCardSeeder.SeedAsync(seedSession);
}

app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();
app.MapScalarDocumentation();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

// 089: PaymentLink (pay-by-link) yüzeyi (store üret/iptal + müşteri açılış-güven-bağlamı + Öde stub).
// Sürüm segmentsiz (dış store kontratı sabit yol). Sağlayıcı callback + HMAC store-bildirim sonraki dilim.
app.AddPaymentLinkEndpointExtension();

// 076: kart-vault + saved-card ödeme uçları SÖKÜLDÜ (card-storage teardown).
// 047: MCP endpoint SÖKÜLDÜ — Payment tool taşımıyordu (hosted-CF ödeme store HTTP çağırır, agent değil).

await app.RunAsync();