using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Identity.Server;

// dotnet ef migrations ... — design-time context (uygulama host'u ayağa kaldırılmaz).
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        options.UseNpgsql("Host=localhost;Port=5432;Database=identityDb;Username=postgres;Password=postgres",
            sql => sql.MigrationsAssembly(typeof(DesignTimeDbContextFactory).Assembly.GetName().Name));
        options.UseOpenIddict();
        return new ApplicationDbContext(options.Options);
    }
}