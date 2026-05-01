var builder = DistributedApplication.CreateBuilder(args);

var rabbitmq = builder
    .AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

var redis = builder
    .AddRedis("redis")
    .WithLifetime(ContainerLifetime.Persistent);

var postgres = builder
    .AddPostgres("postgres")
    .WithPgAdmin()
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var defaultDb = postgres.AddDatabase("defaultDb");

var api = builder.AddProject<Projects.PaymentGatewayApi>("payment-gateway-api")
    .WithReference(rabbitmq)
    .WithReference(redis)
    .WithReference(defaultDb)
    .WaitFor(rabbitmq)
    .WaitFor(redis)
    .WaitFor(defaultDb);

var bff = builder.AddProject<Projects.PaymentGatewayBff>("payment-gateway-bff")
    .WithReference(api)
    .WaitFor(api);

builder.AddProject<Projects.PaymentGatewayPortal>("payment-gateway-portal")
    .WithReference(bff)
    .WaitFor(bff);

builder.Build().Run();