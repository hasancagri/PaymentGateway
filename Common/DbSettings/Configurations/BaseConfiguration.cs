using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.DbSettings.Configurations
{
    public abstract class BaseConfiguration<T> : IEntityConfiguration
          where T : class, IModel
    {
        public virtual string GetSchemaName()
        {
            return string.Empty;
        }

        public virtual string GetTableName()
        {
            return typeof(T).Name;
        }

        public virtual void Map(EntityTypeBuilder<T> model)
        {
            model.HasKey(p => p.Id);
            model.Property(p => p.Id).ValueGeneratedNever();
            model.ToTable(GetTableName(), GetSchemaName());
        }
    }
}
