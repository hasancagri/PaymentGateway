using System.Globalization;
using Admin.Clients;
using Admin.Options;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorPages();
builder.Services.AddMvc(opt => opt.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

// Komisyon grid'inin İLK toplu dolduruşu tüm satırları gönderir (merchant: marka×tip×bölge×taksit
// 1..15 × 5 input ≈ 4800+ değer) — default ValueCountLimit (1024) POST'u 400'e düşürür. Sonraki
// düzenlemeler dirty-cell submit ile küçüktür (filterable-table.js); limit yalnız ilk yükleme için.
// Kalıcı çözüm (kural-bazlı model / feed ingestion) backlog'da — bkz. tasarım notu.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.ValueCountLimit = 8192);

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

// 019: Merchant.Agent A2A chat (komisyon pazarlık ekranı). A2A yüzeyi auth istemez → token handler yok;
// timeout geniş (LLM + MCP tool zinciri tek yanıtta koşar).
builder.Services.AddHttpClient<IMerchantAgentClient, MerchantAgentClient>(client =>
{
    client.BaseAddress = new Uri("http://merchant-agent");
    client.Timeout = TimeSpan.FromMinutes(3);
});

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