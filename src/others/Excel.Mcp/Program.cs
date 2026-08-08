using Common.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// 013: generic spreadsheet üretici MCP server (BC değil, domain bilmez). Yüzey scope-korumalı:
// document.generate (temiz sınır — D12).
builder.Services.AddAuthenticationAndAuthorizationExtension(builder.Configuration, "document.generate");

builder.Services
    .AddMcpServer()
    .WithHttpTransport(o => o.Stateless = true)
    .WithToolsFromAssembly();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();

app.MapMcp("/mcp").RequireAuthorization("document.generate");

await app.RunAsync();