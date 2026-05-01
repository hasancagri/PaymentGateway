using Microsoft.AspNetCore.Components.Authorization;
using PaymentGatewayPortal.Auth;
using PaymentGatewayPortal.Clients;
using PaymentGatewayPortal.Components;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<JwtTokenStore>();
builder.Services.AddScoped<PortalAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<PortalAuthStateProvider>());
builder.Services.AddTransient<JwtAuthHandler>();

builder.Services.AddHttpClient<AuthBffClient>(c =>
    c.BaseAddress = new Uri("https+http://payment-gateway-bff"));

builder.Services.AddHttpClient<MerchantBffClient>(c =>
    c.BaseAddress = new Uri("https+http://payment-gateway-bff"))
    .AddHttpMessageHandler<JwtAuthHandler>();

builder.Services.AddHttpClient<CommissionBffClient>(c =>
    c.BaseAddress = new Uri("https+http://payment-gateway-bff"))
    .AddHttpMessageHandler<JwtAuthHandler>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();