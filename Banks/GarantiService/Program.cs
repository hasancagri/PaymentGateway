using GarantiService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddGrpc();


var app = builder.Build();

app.MapGrpcService<GarantiPaymentService>();
app.MapDefaultEndpoints();

app.Run();