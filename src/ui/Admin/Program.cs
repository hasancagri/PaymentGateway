using System.Globalization;
using Admin.Clients;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorPages();
builder.Services.AddMvc(opt => opt.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

// Typed HttpClient'lar — BaseAddress Aspire service discovery adı (WithReference ile enjekte edilen
// services__<ad>__http__0). Yetki bu dilimde yok; token eklenmez (OIDC sonraki dilim).
builder.Services.AddHttpClient<IMerchantApiClient, MerchantApiClient>(client =>
    client.BaseAddress = new Uri("http://merchant-api"));

builder.Services.AddHttpClient<ICommissionApiClient, CommissionApiClient>(client =>
    client.BaseAddress = new Uri("http://commission-api"));

builder.Services.AddHttpClient<ISettlementAccountApiClient, SettlementAccountApiClient>(client =>
    client.BaseAddress = new Uri("http://merchant-api"));

builder.Services.AddHttpClient<IBinCardApiClient, BinCardApiClient>(client =>
    client.BaseAddress = new Uri("http://payment-api"));

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