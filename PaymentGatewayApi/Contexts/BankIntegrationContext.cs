namespace PaymentGatewayApi.Contexts;

public class BankIntegrationContext : BaseDbContext<BankIntegrationContext>
{
    public BankIntegrationContext(IServiceProvider serviceProvider, DbContextOptions<BankIntegrationContext> option,
        ICurrentUser currentUser) : base(serviceProvider, option, currentUser)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}