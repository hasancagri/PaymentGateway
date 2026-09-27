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
var identityDb = postgres.AddDatabase("identityDb");

// 011: OpenIddict IdP — sabit https://localhost:5101 (launchSettings https profili; issuer birebir).
// BC API'leri token'ı JWKS ile doğrular; Admin/Agent client_credentials token'ı buradan alır.
// 012: merchant.lifecycle fanout'unu tüketir (merchant → OpenIddict istemci senkronu).
var identityServer = builder.AddProject<Projects.Payment_Identity>("identity-server", launchProfileName: "https")
    .WithReference(identityDb)
    .WithReference(rabbit)
    .WaitFor(identityDb)
    .WaitFor(rabbit);

builder.AddProject<Projects.Payment_Api>("payment-api")
    .WithReference(paymentDb)
    .WithReference(rabbit)
    .WithReference(identityServer)
    .WaitFor(paymentDb)
    .WaitFor(rabbit)
    .WaitFor(identityServer);

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

var merchantApi = builder.AddProject<Projects.Merchant_Api>("merchant-api")
    .WithReference(merchantDb)
    .WithReference(rabbit)
    .WithReference(identityServer)
    .WaitFor(merchantDb)
    .WaitFor(rabbit)
    .WaitFor(identityServer);

builder.AddProject<Projects.Commission_Api>("commission-api")
    .WithReference(commissionDb)
    .WithReference(rabbit)
    .WithReference(identityServer)
    .WaitFor(commissionDb)
    .WaitFor(rabbit)
    .WaitFor(identityServer);

// 013: Identity aktivasyon sayfası Merchant.Api redeem'i senkron çağırır (sanksiyonlu). Service
// discovery için referans (WaitFor YOK → merchant-api zaten identity'yi beklediğinden döngü olmaz).
identityServer.WithReference(merchantApi);

// 048: Razor Admin BFF (admin-web) SÖKÜLDÜ — hassas-veri kanalı artık Merchant.Api'nin hosted
// link'i (agent merchant.admin MCP tool ile üretir; insan tarayıcıda açar). CRUD ekranları 044'te
// zaten söküktü; kalan tek iş yüzeyi hassas-veri sayfası da bu hosted link'e taşındı.

builder.Build().Run();