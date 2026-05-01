using PaymentGatewayBff.Clients;
using PaymentGatewayBff.Endpoints.Mobile;
using PaymentGatewayBff.Endpoints.Web;
using PaymentGatewayBff.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<AuthForwardingHandler>();

builder.Services.AddHttpClient<AuthApiClient>(c =>
    c.BaseAddress = new Uri("https+http://payment-gateway-api"));

builder.Services.AddHttpClient<MerchantApiClient>(c =>
    c.BaseAddress = new Uri("https+http://payment-gateway-api"))
    .AddHttpMessageHandler<AuthForwardingHandler>();

builder.Services.AddHttpClient<CommissionApiClient>(c =>
    c.BaseAddress = new Uri("https+http://payment-gateway-api"))
    .AddHttpMessageHandler<AuthForwardingHandler>();

var app = builder.Build();
app.MapDefaultEndpoints();

var web = app.MapGroup("/web");
web.MapWebAuthEndpoints();
web.MapWebMerchantEndpoints();
web.MapWebCommissionEndpoints();

var mobile = app.MapGroup("/mobile");
mobile.MapMobileMerchantEndpoints();
mobile.MapMobileCommissionEndpoints();

app.Run();