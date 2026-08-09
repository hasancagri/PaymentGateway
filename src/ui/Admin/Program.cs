using System.Globalization;
using Admin.Clients;
using Admin.Options;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorPages();
builder.Services.AddMvc(opt => opt.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

// Config → strongly-typed POCO (runtime doğrudan IConfiguration okuması yasak; CLAUDE.md).
builder.Services.AddOptions<IdentityOption>().BindConfiguration(nameof(IdentityOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<IdentityOption>(sp => sp.GetRequiredService<IOptions<IdentityOption>>().Value);
builder.Services.AddOptions<AdminAuth>().BindConfiguration(nameof(AdminAuth))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<AdminAuth>(sp => sp.GetRequiredService<IOptions<AdminAuth>>().Value);

// Typed HttpClient'lar — BaseAddress Aspire service discovery adı (WithReference ile enjekte edilen
// services__<ad>__http__0). 011: her istek AdminTokenHandler ile Bearer taşır (client_credentials).
builder.Services.AddTransient<AdminTokenHandler>();

builder.Services.AddHttpClient<IMerchantApiClient, MerchantApiClient>(client =>
        client.BaseAddress = new Uri("http://merchant-api"))
    .AddHttpMessageHandler<AdminTokenHandler>();

builder.Services.AddHttpClient<ICommissionApiClient, CommissionApiClient>(client =>
        client.BaseAddress = new Uri("http://commission-api"))
    .AddHttpMessageHandler<AdminTokenHandler>();

builder.Services.AddHttpClient<ISettlementAccountApiClient, SettlementAccountApiClient>(client =>
        client.BaseAddress = new Uri("http://merchant-api"))
    .AddHttpMessageHandler<AdminTokenHandler>();

builder.Services.AddHttpClient<IRegisterRequestApiClient, RegisterRequestApiClient>(client =>
        client.BaseAddress = new Uri("http://merchant-api"))
    .AddHttpMessageHandler<AdminTokenHandler>();

builder.Services.AddHttpClient<IBinCardApiClient, BinCardApiClient>(client =>
        client.BaseAddress = new Uri("http://payment-api"))
    .AddHttpMessageHandler<AdminTokenHandler>();

var app = builder.Build();

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseRouting();

// Ondalık form bağlaması invariant olsun: HTML number input'ları hep "." ondalık gönderir;
// sunucu tr-TR kültüründe "." grup ayıracı sanıp "4.05"i 405 yapıyordu. Invariant ile eşleşir.
// UI metinleri sabit Türkçe (resx yok), bu değişiklikten etkilenmez.
var invariantCultures = new[] { CultureInfo.InvariantCulture };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture),
    SupportedCultures = invariantCultures,
    SupportedUICultures = invariantCultures
});

app.MapStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();

await app.RunAsync();