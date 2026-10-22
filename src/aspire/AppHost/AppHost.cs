var builder = DistributedApplication.CreateBuilder(args);

// Yayın hedefi (publish/push-only): `aspire do push` her AddProject'i container image'ine çevirip ghcr'a iter.
// Yalnız publish/push'ta devreye girer — yerel `aspire run` dev akışı değişmez.
builder.AddDockerComposeEnvironment("compose");

// Otopark (registry) = ghcr.io/hasancagri/paymentgateway. PREFIX ZORUNLU: identity-server/payment-api
// isimleri AgentPlatform + ECommerce ile çakışır; repo-başına ayrı namespace → çakışma yok.
var ghcr = builder.AddContainerRegistry("ghcr", "ghcr.io", "hasancagri/paymentgateway");

// Host portu sabit 5433 (varsayılan 5432 değil) — başka bir Aspire uygulaması da PostgreSQL'i
// Aspire üzerinden kaldırdığı için port çakışmasını önler.
var postgres = builder.AddPostgres("postgres", port: 5433)
    .WithPgAdmin()
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var rabbit = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

var paymentDb = postgres.AddDatabase("paymentDb");

// Kimlik birleşimi (2026-10-22): PG kendi OpenIddict IdP'sini BIRAKTI. EC→PG = paylaşılan API key;
// gelecek operatör/MCP/ops JWT yüzeyi için AgentPlatform IdP (PlatformIdentityOption seam). identity-server
// resource + identityDb SÖKÜLDÜ — PG artık tek başına (dev-time IdP bağımlılığı yok).
var paymentApi = builder.AddProject<Projects.Payment_Api>("payment-api")
    .WithReference(paymentDb)
    .WithReference(rabbit)
    .WaitFor(paymentDb)
    .WaitFor(rabbit);

// Yaşayan deploy edilebilir servisi ghcr'a bağla. `aspire do push` image'ini basıp
// ghcr.io/hasancagri/paymentgateway/payment-api'ye iter. Altyapı (postgres/rabbit) public → girmez.
paymentApi.WithContainerRegistry(ghcr);

builder.Build().Run();
