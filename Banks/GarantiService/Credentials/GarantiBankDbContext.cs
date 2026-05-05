namespace GarantiService.Credentials;

public class GarantiBankDbContext(DbContextOptions<GarantiBankDbContext> options) : DbContext(options)
{
    public DbSet<MerchantBankCredential> Credentials => Set<MerchantBankCredential>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<MerchantBankCredential>(e =>
        {
            e.ToTable("MerchantBank", "bankIntegration");
            e.HasKey(x => x.Id);
        });
    }
}