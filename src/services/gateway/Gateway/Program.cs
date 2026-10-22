var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Cluster adresleri Aspire service discovery adlari (http://catalog-api gibi).
// AddServiceDiscoveryDestinationResolver: YARP bu adlari ServiceDefaults'in service
// discovery'si uzerinden (services__<ad>__http__0) gercek endpoint'e cozer.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

// Kimlik birleşimi (2026-10-22): PG kendi IdP'sini bıraktı → Gateway JWT auth + policy'leri SÖKÜLDÜ.
// Gateway salt pass-through proxy; auth enforcement downstream'de (Payment.Api = API key). X-Api-Key
// header'ı YARP tarafından olduğu gibi iletilir. (Gateway bugün AppHost/k8s'te wire DEĞİL — dormant.)
// NOT: appsettings ReverseProxy route'larındaki AuthorizationPolicy referansları dormant; Gateway
// canlıya alınırsa route-policy temizliği + downstream key iletimi ayrı ele alınır.
var app = builder.Build();

app.MapDefaultEndpoints();
app.MapReverseProxy();
app.MapGet("/", () => "YARP (Gateway)");
await app.RunAsync();