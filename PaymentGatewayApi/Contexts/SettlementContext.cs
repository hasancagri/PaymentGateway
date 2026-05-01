namespace PaymentGatewayApi.Contexts;

public class SettlementContext : BaseDbContext<SettlementContext>
{
    public SettlementContext(IServiceProvider serviceProvider, DbContextOptions<SettlementContext> option,
        ICurrentUser currentUser) : base(serviceProvider, option, currentUser)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}