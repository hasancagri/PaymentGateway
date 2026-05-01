namespace PaymentGatewayApi.Contexts;

public class CommissionManagementContext : BaseDbContext<CommissionManagementContext>
{
    public CommissionManagementContext(IServiceProvider serviceProvider, DbContextOptions<CommissionManagementContext> option,
        ICurrentUser currentUser) : base(serviceProvider, option, currentUser)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}