namespace Common.DbSettings.Configurations
{
    public interface IEntityConfiguration : ITransientDependency
    {
        string GetTableName();
        string GetSchemaName();
    }

    public interface IEntityConfiguration<TContext> : IEntityConfiguration
        where TContext : DbContext
    {
    }
}
