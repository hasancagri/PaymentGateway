using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Identity.Server.Connect;

// Açılışta idempotent scope + client seed (varsa güncelle, yoksa yarat). Yalnız Config'teki
// statik listeye dokunur — G2'nin çalışma anında ekleyeceği merchant client'ları EZİLMEZ (D4/D9).
public sealed class SeedHostedService(IServiceProvider provider, IConfiguration configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = provider.CreateScope();
        var apps = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var scopes = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

        // Scope'lar (audience/resource eşlemesiyle) — ListResourcesAsync bunlardan 'aud' üretir.
        foreach (var name in Config.AllApiScopes)
        {
            var descriptor = new OpenIddictScopeDescriptor { Name = name, DisplayName = name };
            if (Config.ScopeResources.TryGetValue(name, out var resource))
                descriptor.Resources.Add(resource);

            var existing = await scopes.FindByNameAsync(name, ct);
            if (existing is null)
                await scopes.CreateAsync(descriptor, ct);
            else
                await scopes.UpdateAsync(existing, descriptor, ct);
        }

        // İstemciler (secret config'ten; store hash'ler).
        foreach (var client in Config.Clients(configuration))
        {
            var descriptor = BuildDescriptor(client);
            var existing = await apps.FindByClientIdAsync(client.ClientId, ct);
            if (existing is null)
                await apps.CreateAsync(descriptor, ct);
            else
                await apps.UpdateAsync(existing, descriptor, ct);
        }

        // 044 R5: sökülen client'lar store'dan da silinir — seed create/update'li olduğundan
        // liste-dışı kalmak yetmez, kayıt durursa ölü kimlik token almaya devam eder (fail-closed).
        foreach (var retired in Config.RetiredClientIds)
        {
            var dead = await apps.FindByClientIdAsync(retired, ct);
            if (dead is not null)
                await apps.DeleteAsync(dead, ct);
        }

        // G3: bootstrap admin — yalnız config doluysa VE kullanıcı yoksa oluşturulur (idempotent;
        // sonradan admin'in değiştirdiği parola ezilmez).
        var bootstrapAdmin = scope.ServiceProvider.GetRequiredService<Identity.Server.Options.BootstrapAdmin>();
        if (!string.IsNullOrWhiteSpace(bootstrapAdmin.Email) && !string.IsNullOrWhiteSpace(bootstrapAdmin.Password))
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (await userManager.FindByNameAsync(bootstrapAdmin.Email) is null)
            {
                var admin = new ApplicationUser
                {
                    UserName = bootstrapAdmin.Email,
                    Email = bootstrapAdmin.Email,
                    EmailConfirmed = true,
                };
                var created = await userManager.CreateAsync(admin, bootstrapAdmin.Password);
                if (!created.Succeeded)
                    throw new InvalidOperationException(
                        $"Bootstrap admin oluşturulamadı: {string.Join("; ", created.Errors.Select(e => e.Description))}");
            }
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private static OpenIddictApplicationDescriptor BuildDescriptor(ClientSeed client)
    {
        var d = new OpenIddictApplicationDescriptor
        {
            ClientId = client.ClientId,
            ClientSecret = client.IsPublic ? null : client.ClientSecret,
            DisplayName = client.DisplayName,
            ClientType = client.IsPublic ? ClientTypes.Public : ClientTypes.Confidential,
            // Seed istemciler Implicit (consent yok) — DCR henüz yok, hepsi ilk-taraf.
            ConsentType = ConsentTypes.Implicit,
        };

        if (client.AllowAuthorizationCode)
        {
            d.Permissions.Add(Permissions.Endpoints.Authorization);
            d.Permissions.Add(Permissions.GrantTypes.AuthorizationCode);
            d.Permissions.Add(Permissions.ResponseTypes.Code);
            d.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
        }

        // Mevcut 5 M2M istemci AllowAuthorizationCode=false ile gelir → hepsi client_credentials
        // permission'ı alır (REGRESYON YOK). external-admin-agent AllowAuthorizationCode=true
        // olduğundan bu dala GİRMEZ — public istemciye client_credentials permission'ı eklenmez.
        if (!client.AllowAuthorizationCode)
            d.Permissions.Add(Permissions.GrantTypes.ClientCredentials);

        if (client.AllowRefreshToken)
            d.Permissions.Add(Permissions.GrantTypes.RefreshToken);

        d.Permissions.Add(Permissions.Endpoints.Token);

        foreach (var uri in client.RedirectUris)
            d.RedirectUris.Add(new Uri(uri));

        // Scope izinleri (scp: prefix'li) — istenen scope ⊆ bu küme, aksi invalid_scope.
        foreach (var s in client.Scopes)
            d.Permissions.Add(Permissions.Prefixes.Scope + s);

        return d;
    }
}