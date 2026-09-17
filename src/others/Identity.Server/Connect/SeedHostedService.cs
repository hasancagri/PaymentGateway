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
            ClientSecret = client.ClientSecret,
            DisplayName = client.DisplayName,
            ClientType = ClientTypes.Confidential,
            ConsentType = ConsentTypes.Implicit,
        };

        // 011 tek grant: client_credentials + token ucu.
        d.Permissions.Add(Permissions.GrantTypes.ClientCredentials);
        d.Permissions.Add(Permissions.Endpoints.Token);

        // Scope izinleri (scp: prefix'li) — istenen scope ⊆ bu küme, aksi invalid_scope.
        foreach (var s in client.Scopes)
            d.Permissions.Add(Permissions.Prefixes.Scope + s);

        return d;
    }
}