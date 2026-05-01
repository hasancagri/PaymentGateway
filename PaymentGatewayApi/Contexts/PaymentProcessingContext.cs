namespace PaymentGatewayApi.Contexts;

public class PaymentProcessingContext : BaseDbContext<PaymentProcessingContext>
{
    public PaymentProcessingContext(IServiceProvider serviceProvider, DbContextOptions<PaymentProcessingContext> option,
        ICurrentUser currentUser) : base(serviceProvider, option, currentUser)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}