var builder = DistributedApplication.CreateBuilder(args);

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
var merchantDb = postgres.AddDatabase("merchantDb");
var commissionDb = postgres.AddDatabase("commissionDb");

var paymentApi = builder.AddProject<Projects.Payment_Api>("payment-api")
    .WithReference(paymentDb)
    .WithReference(rabbit)
    .WaitFor(paymentDb)
    .WaitFor(rabbit);

var merchantApi = builder.AddProject<Projects.Merchant_Api>("merchant-api")
    .WithReference(merchantDb)
    .WithReference(rabbit)
    .WaitFor(merchantDb)
    .WaitFor(rabbit);

var commissionApi = builder.AddProject<Projects.Commission_Api>("commission-api")
    .WithReference(commissionDb)
    .WithReference(rabbit)
    .WaitFor(commissionDb)
    .WaitFor(rabbit);

// Admin BFF (Razor Pages) — iki API'yi service discovery ile çağırır (http://merchant-api,
// http://commission-api). Yetki bu dilimde yok.
builder.AddProject<Projects.Admin>("admin-web")
    .WithReference(merchantApi)
    .WithReference(commissionApi)
    .WithReference(paymentApi)
    .WaitFor(merchantApi)
    .WaitFor(commissionApi)
    .WaitFor(paymentApi);

builder.Build().Run();