using Admin.Clients;

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

var app = builder.Build();

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseRouting();

app.MapStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();

await app.RunAsync();