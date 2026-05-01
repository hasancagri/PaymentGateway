using Common.DbSettings.Configurations;

namespace Common.DbSettings;

public class BaseDbContext<TContext> : DbContext
    where TContext : DbContext
{
    private readonly IServiceProvider _provider;
    private readonly ICurrentUser _currentUser;

    public BaseDbContext(IServiceProvider serviceProvider, DbContextOptions<TContext> option,
        ICurrentUser currentUser) : base(option)
    {
        _currentUser = currentUser;
        _provider = serviceProvider;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var entityMethod = modelBuilder.GetType().GetTypeInfo().GetMethods()
            .First(p => p is { Name: nameof(ModelBuilder.Entity), IsGenericMethod: true });

        var configurations = _provider.GetServices<IEntityConfiguration<TContext>>();

        foreach (var item in configurations)
        {
            var typeInfo = item.GetType().GetTypeInfo();
            var mapMethod = typeInfo.GetMethod(nameof(BaseConfiguration<BaseModel>.Map));
            var baseTypeInfo = typeInfo.BaseType!.GetTypeInfo();
            var entityGenericType = baseTypeInfo.GenericTypeArguments[0];
            var genericMethod = entityMethod.MakeGenericMethod(entityGenericType);
            var methodResult = genericMethod.Invoke(modelBuilder, null);
            mapMethod?.Invoke(item, new object[] { methodResult! });
        }

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.FindProperty("IsDeleted") == null)
                continue;

            var parameter = Expression.Parameter(entityType.ClrType);

            // EF.Property<bool>(post, "IsDeleted")
            var propertyMethodInfo = typeof(EF).GetMethod("Property")!.MakeGenericMethod(typeof(bool));
            var isDeletedProperty = Expression.Call(propertyMethodInfo, parameter, Expression.Constant("IsDeleted"));

            // EF.Property<bool>(post, "IsDeleted") == false
            BinaryExpression compareExpression =
                Expression.MakeBinary(ExpressionType.Equal, isDeletedProperty, Expression.Constant(false));

            // post => EF.Property<bool>(post, "IsDeleted") == false
            var lambda = Expression.Lambda(compareExpression, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }


    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        OnBeforeSaving();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        OnBeforeSaving();
        return base.SaveChanges();
    }

    private void OnBeforeSaving()
    {
        foreach (var entry in ChangeTracker.Entries<IModel>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.CurrentValues["CreatedTime"] = DateHelper.Now();
                    entry.CurrentValues["IsDeleted"] = false;
                    break;
                case EntityState.Modified:
                    entry.CurrentValues["UpdatedTime"] = DateHelper.Now();
                    entry.CurrentValues["IsDeleted"] = false;
                    break;
                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.CurrentValues["IsDeleted"] = true;
                    entry.CurrentValues["DeletedTime"] = DateHelper.Now();
                    break;
                case EntityState.Detached:
                    break;
                case EntityState.Unchanged:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        if (_currentUser.Id == Guid.Empty) return;
        {
            foreach (var entry in ChangeTracker.Entries<IUserTrackModel>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.CurrentValues["CreatedUserId"] = _currentUser.Id;
                        break;
                    case EntityState.Modified:
                        entry.CurrentValues["UpdatedUserId"] = _currentUser.Id;
                        break;
                    case EntityState.Deleted:
                        entry.CurrentValues["DeletedUserId"] = _currentUser.Id;
                        break;
                    case EntityState.Detached:
                        break;
                    case EntityState.Unchanged:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
        
    }
}