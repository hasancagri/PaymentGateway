var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var rabbit = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

var paymentDb = postgres.AddDatabase("paymentDb");

builder.AddProject<Projects.Payment_Api>("payment-api")
    .WithReference(paymentDb)
    .WithReference(rabbit)
    .WaitFor(paymentDb)
    .WaitFor(rabbit);

builder.Build().Run();