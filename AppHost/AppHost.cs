var builder = DistributedApplication.CreateBuilder(args);

var rabbitmq = builder.AddRabbitMQ("rabbitmq").WithManagementPlugin().WithLifetime(ContainerLifetime.Persistent);
var redis = builder.AddRedis("redis").WithLifetime(ContainerLifetime.Persistent);
var postgres = builder.AddPostgres("postgres").WithPgAdmin().WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

// Dedicated database per service (Database per Service pattern)
var garantiDb = postgres.AddDatabase("defaultDb"); // kept for GarantiService
var merchantDb = postgres.AddDatabase("merchantDb");
var paymentDb = postgres.AddDatabase("paymentDb");
var bankIntDb = postgres.AddDatabase("bankIntegrationDb");
var commissionDb = postgres.AddDatabase("commissionDb");
var iamDb = postgres.AddDatabase("iamDb");
var settlementDb = postgres.AddDatabase("settlementDb");
var gatewayDb = postgres.AddDatabase("gatewayDb"); // Wolverine outbox for ApiGateway

// JWT secret shared across ApiGateway and all downstream services
var jwtSecret = builder.AddParameter("jwt-secret", secret: true);

var garanti = builder.AddProject<Projects.GarantiService>("garanti")
    .WithReference(garantiDb).WaitFor(garantiDb);

var merchantApi = builder.AddProject<Projects.MerchantManagement_Api>("merchant-management")
    .WithReference(rabbitmq).WithReference(merchantDb)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WaitFor(rabbitmq).WaitFor(merchantDb);

var gateway = builder.AddProject<Projects.ApiGateway>("api-gateway")
    .WithReference(rabbitmq).WithReference(redis).WithReference(gatewayDb)
    .WithReference(merchantApi)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WaitFor(rabbitmq).WaitFor(redis).WaitFor(gatewayDb).WaitFor(merchantApi);

var bankIntApi = builder.AddProject<Projects.BankIntegration_Api>("bank-integration")
    .WithReference(rabbitmq).WithReference(bankIntDb)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WaitFor(rabbitmq).WaitFor(bankIntDb);

var commissionApi = builder.AddProject<Projects.CommissionManagement_Api>("commission-management")
    .WithReference(rabbitmq).WithReference(commissionDb)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WaitFor(rabbitmq).WaitFor(commissionDb);

var paymentApi = builder.AddProject<Projects.PaymentProcessing_Api>("payment-processing")
    .WithReference(rabbitmq).WithReference(paymentDb).WithReference(garanti)
    .WithReference(bankIntApi).WithReference(commissionApi)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WaitFor(rabbitmq).WaitFor(paymentDb).WaitFor(garanti).WaitFor(bankIntApi).WaitFor(commissionApi);

var iamApi = builder.AddProject<Projects.IAM_Api>("iam")
    .WithReference(rabbitmq).WithReference(iamDb).WithReference(redis)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WaitFor(rabbitmq).WaitFor(iamDb).WaitFor(redis);

var settlementApi = builder.AddProject<Projects.Settlement_Api>("settlement")
    .WithReference(rabbitmq).WithReference(settlementDb)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WaitFor(rabbitmq).WaitFor(settlementDb);

builder.Build().Run();