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
var referenceDb = postgres.AddDatabase("referenceDb");

var paymentApi = builder.AddProject<Projects.Payment_Api>("payment-api")
    .WithReference(paymentDb)
    .WithReference(rabbit)
    .WaitFor(paymentDb)
    .WaitFor(rabbit);

// Referans veri kaynak-of-truth BC. HTTP yüzeyi yok — katalog verisini yalnız ReferenceDataUpdated
// fanout event'iyle yayar; Merchant/Commission durable queue ile tüketir (yerel read-model).
builder.AddProject<Projects.Reference_Api>("reference-api")
    .WithReference(referenceDb)
    .WithReference(rabbit)
    .WaitFor(referenceDb)
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

// 007 A2A: Payment.Agent — A2A host + LLM router + MCP client. BC değil, stateless delivery
// adaptörü. payment-api'nin MCP endpoint'ini (http://payment-api/mcp) service discovery ile bulur.
// Chat model anahtarı agent'ın kendi config'inden (OpenAI:ApiKey / user-secrets) — ECommerce deseni.
builder.AddProject<Projects.Payment_Agent>("payment-agent")
    .WithReference(paymentApi)
    .WaitFor(paymentApi);

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