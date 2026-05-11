var builder = DistributedApplication.CreateBuilder(args);

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

var redis = builder.AddRedis("redis")
    .WithLifetime(ContainerLifetime.Persistent);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var iamDb = postgres.AddDatabase("iamDb");
var merchantDb = postgres.AddDatabase("merchantDb");
var paymentDb = postgres.AddDatabase("paymentDb");
var bankIntDb = postgres.AddDatabase("bankIntegrationDb");
var commissionDb = postgres.AddDatabase("commissionDb");

var settlementDb = postgres.AddDatabase("settlementDb");


var garanti = builder.AddProject<Projects.GarantiService>("garanti");

var merchantApi = builder.AddProject<Projects.MerchantManagement_Api>("merchant-management")
    .WithReference(rabbitmq).WithReference(merchantDb)
    .WaitFor(rabbitmq).WaitFor(merchantDb);

var bankIntApi = builder.AddProject<Projects.BankIntegration_Api>("bank-integration")
    .WithReference(rabbitmq).WithReference(bankIntDb)
    .WaitFor(rabbitmq).WaitFor(bankIntDb);

var commissionApi = builder.AddProject<Projects.CommissionManagement_Api>("commission-management")
    .WithReference(rabbitmq).WithReference(commissionDb)
    .WaitFor(rabbitmq).WaitFor(commissionDb);

var paymentApi = builder.AddProject<Projects.PaymentProcessing_Api>("payment-processing")
    .WithReference(rabbitmq).WithReference(paymentDb).WithReference(garanti)
    .WithReference(bankIntApi).WithReference(commissionApi)
    .WaitFor(rabbitmq).WaitFor(paymentDb).WaitFor(garanti).WaitFor(bankIntApi).WaitFor(commissionApi);

var iamApi = builder.AddProject<Projects.IAM_Api>("iam")
    .WithReference(rabbitmq).WithReference(iamDb).WithReference(redis)
    .WaitFor(rabbitmq).WaitFor(iamDb).WaitFor(redis);

var settlementApi = builder.AddProject<Projects.Settlement_Api>("settlement")
    .WithReference(rabbitmq).WithReference(settlementDb)
    .WaitFor(rabbitmq).WaitFor(settlementDb);

builder.Build().Run();