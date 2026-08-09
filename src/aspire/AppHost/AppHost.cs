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
var identityDb = postgres.AddDatabase("identityDb");

// 011: OpenIddict IdP — sabit https://localhost:5101 (launchSettings https profili; issuer birebir).
// BC API'leri token'ı JWKS ile doğrular; Admin/Agent client_credentials token'ı buradan alır.
// 012: merchant.lifecycle fanout'unu tüketir (merchant → OpenIddict istemci senkronu).
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

// Referans veri kaynak-of-truth BC. HTTP yüzeyi yok — katalog verisini yalnız ReferenceDataUpdated
// fanout event'iyle yayar; Merchant/Commission durable queue ile tüketir (yerel read-model).
builder.AddProject<Projects.Reference_Api>("reference-api")
    .WithReference(referenceDb)
    .WithReference(rabbit)
    .WaitFor(referenceDb)
    .WaitFor(rabbit);

// 013: Mailpit — dev SMTP catch-all (SMTP :1025, web UI :8025). Gerçek adres gerekmez; tüm
// giden mail tek inbox'ta görünür. Mail.Worker buraya SMTP ile bağlanır (localhost:1025).
var mailpit = builder.AddContainer("mailpit", "axllent/mailpit")
    .WithEndpoint(port: 1025, targetPort: 1025, name: "smtp")
    .WithHttpEndpoint(port: 8025, targetPort: 8025, name: "http")
    .WithLifetime(ContainerLifetime.Persistent);

// 016: Mail.Worker = düz mail projesi (MCP DEĞİL). mail.delivery fanout'unu RabbitMQ ile tüketip
// SMTP (Mailpit) ile gönderir. Auth yok (HTTP yüzeyi yok); yalnız kuyruk consumer'ı.
builder.AddProject<Projects.Mail_Worker>("mail-worker")
    .WithReference(rabbit)
    .WaitFor(rabbit)
    .WaitFor(mailpit);

// 013: generic altyapı MCP server'ı (BC değil). Excel.Mcp = generate_spreadsheet (.xlsx).
// Yüzey scope-korumalı (document.generate); yalnız Agent/LLM çağırır.
builder.AddProject<Projects.Excel_Mcp>("excel-mcp")
    .WithReference(identityServer)
    .WaitFor(identityServer);

var merchantApi = builder.AddProject<Projects.Merchant_Api>("merchant-api")
    .WithReference(merchantDb)
    .WithReference(rabbit)
    .WithReference(identityServer)
    .WaitFor(merchantDb)
    .WaitFor(rabbit)
    .WaitFor(identityServer);

var commissionApi = builder.AddProject<Projects.Commission_Api>("commission-api")
    .WithReference(commissionDb)
    .WithReference(rabbit)
    .WithReference(identityServer)
    .WaitFor(commissionDb)
    .WaitFor(rabbit)
    .WaitFor(identityServer);

// 007 A2A: Payment.Agent — A2A host + LLM router + MCP client. BC değil, stateless delivery
// adaptörü. payment-api'nin MCP endpoint'ini (http://payment-api/mcp) service discovery ile bulur.
// Chat model anahtarı agent'ın kendi config'inden (OpenAI:ApiKey / user-secrets) — ECommerce deseni.
builder.AddProject<Projects.Payment_Agent>("payment-agent")
    .WithReference(paymentApi)
    .WithReference(identityServer)
    .WaitFor(paymentApi)
    .WaitFor(identityServer);

// 013: Identity aktivasyon sayfası Merchant.Api redeem'i senkron çağırır (sanksiyonlu). Service
// discovery için referans (WaitFor YOK → merchant-api zaten identity'yi beklediğinden döngü olmaz).
identityServer.WithReference(merchantApi);

// 013 A2A: Merchant.Agent — merchant adaylarının kayıt başvurusunu A2A ile alır (Payment.Agent
// deseni). BC değil, stateless. merchant-api'nin MCP endpoint'ini service discovery ile bulur.
// Chat model anahtarı agent'ın kendi config'inden (OpenAI:ApiKey / user-secrets).
builder.AddProject<Projects.Merchant_Agent>("merchant-agent")
    .WithReference(merchantApi)
    .WithReference(identityServer)
    .WaitFor(merchantApi)
    .WaitFor(identityServer);

// Admin BFF (Razor Pages) — üç API'yi service discovery ile çağırır; 011: her istek
// AdminTokenHandler ile makine token'ı taşır (admin-ui client'ı, Identity.Server'dan).
builder.AddProject<Projects.Admin>("admin-web")
    .WithReference(merchantApi)
    .WithReference(commissionApi)
    .WithReference(paymentApi)
    .WithReference(identityServer)
    .WaitFor(merchantApi)
    .WaitFor(commissionApi)
    .WaitFor(paymentApi)
    .WaitFor(identityServer);

builder.Build().Run();