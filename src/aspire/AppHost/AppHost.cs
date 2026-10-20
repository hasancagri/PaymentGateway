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
var identityDb = postgres.AddDatabase("identityDb");

// 011: OpenIddict IdP — sabit https://localhost:5101 (launchSettings https profili; issuer birebir).
// BC API'leri token'ı JWKS ile doğrular; makine client_credentials token'ı buradan alır.
var identityServer = builder.AddProject<Projects.Identity_Server>("identity-server", launchProfileName: "https")
    .WithReference(identityDb)
    .WithReference(rabbit)
    .WaitFor(identityDb)
    .WaitFor(rabbit);

var paymentApi = builder.AddProject<Projects.Payment_Api>("payment-api")
    .WithReference(paymentDb)
    .WithReference(rabbit)
    .WithReference(identityServer)
    .WaitFor(paymentDb)
    .WaitFor(rabbit)
    .WaitFor(identityServer);

// Yaşayan deploy edilebilir servisleri ghcr'a bağla. `aspire do push` image'lerini basıp
// ghcr.io/hasancagri/paymentgateway/<servis>'e iter. Altyapı (postgres/rabbit) public → girmez.
foreach (var svc in new[]
{
    identityServer, paymentApi
})
{
    svc.WithContainerRegistry(ghcr);
}

builder.Build().Run();
