using Common.Extensions;
using Mail.Mcp.Options;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// 013: generic mail relay MCP server (BC değil, domain bilmez). Yüzey scope-korumalı: mail.send.
builder.Services.AddAuthenticationAndAuthorizationExtension(builder.Configuration, "mail.send");

// SMTP ayarları POCO (runtime doğrudan IConfiguration okuması yasak; CLAUDE.md).
builder.Services.AddOptions<Mail.Mcp.Options.Mail>().BindConfiguration("Mail")
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Mail.Mcp.Options.Mail>(sp =>
    sp.GetRequiredService<IOptions<Mail.Mcp.Options.Mail>>().Value);

builder.Services
    .AddMcpServer()
    .WithHttpTransport(o => o.Stateless = true)
    .WithToolsFromAssembly();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();

// Tek tool yüzeyi (send_email) — mail.send scope'lu token zorunlu.
app.MapMcp("/mcp").RequireAuthorization("mail.send");

await app.RunAsync();