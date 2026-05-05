using GarantiService;
using GarantiService.Credentials;
using GarantiService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddGrpc();

builder.Services.AddHttpClient("Garanti", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Garanti:ApiUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "text/xml");
});

builder.AddNpgsqlDbContext<GarantiBankDbContext>("defaultDb");

builder.Services.AddScoped<GarantiHttpClient>();

var app = builder.Build();

app.MapGrpcService<GarantiPaymentService>();
app.MapDefaultEndpoints();

app.Run();