namespace MerchantManagement.Api;

public class MerchantManagementContext : BaseDbContext<MerchantManagementContext>
{
    public MerchantManagementContext(IServiceProvider serviceProvider, DbContextOptions<MerchantManagementContext> option,
        ICurrentUser currentUser) : base(serviceProvider, option, currentUser)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}