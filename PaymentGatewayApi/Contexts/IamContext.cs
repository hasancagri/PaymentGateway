namespace PaymentGatewayApi.Contexts;

public class IamContext : BaseDbContext<IamContext>
{
    public IamContext(IServiceProvider serviceProvider, DbContextOptions<IamContext> option,
        ICurrentUser currentUser) : base(serviceProvider, option, currentUser)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}