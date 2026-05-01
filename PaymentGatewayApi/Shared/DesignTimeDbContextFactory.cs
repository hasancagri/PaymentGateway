using Microsoft.EntityFrameworkCore.Design;

namespace PaymentGatewayApi.Shared;

public abstract class DesignTimeDbContextFactory<TContext> : IDesignTimeDbContextFactory<TContext>
    where TContext : BaseDbContext<TContext>
{
    public TContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("defaultDb")
            ?? "Host=localhost;Database=paymentgateway;Username=postgres;Password=postgres";

        var services = new ServiceCollection();

        services.Scan(scan => scan
            .FromAssemblyOf<TContext>()
            .AddClasses(classes => classes.AssignableTo<IEntityConfiguration<TContext>>())
            .AsImplementedInterfaces()
            .WithTransientLifetime());

        services.AddTransient<ICurrentUser, DesignTimeCurrentUser>();

        var optionsBuilder = new DbContextOptionsBuilder<TContext>();
        optionsBuilder.UseNpgsql(connectionString);

        var serviceProvider = services.BuildServiceProvider();

        return (TContext)Activator.CreateInstance(
            typeof(TContext),
            serviceProvider,
            optionsBuilder.Options,
            serviceProvider.GetRequiredService<ICurrentUser>())!;
    }
}

public class BankIntegrationContextFactory : DesignTimeDbContextFactory<BankIntegrationContext> { }
public class CommissionManagementContextFactory : DesignTimeDbContextFactory<CommissionManagementContext> { }
public class IamContextFactory : DesignTimeDbContextFactory<IamContext> { }
public class MerchantManagementContextFactory : DesignTimeDbContextFactory<MerchantManagementContext> { }
public class PaymentProcessingContextFactory : DesignTimeDbContextFactory<PaymentProcessingContext> { }
public class SettlementContextFactory : DesignTimeDbContextFactory<SettlementContext> { }