using PaymentGatewayApi.Modules.IAM.Roles.Entities;
using PaymentGatewayApi.Modules.IAM.Roles.ValueObjects;

namespace PaymentGatewayApi.Modules.IAM.Roles;

public class RoleConfiguration
    : BaseConfiguration<Role>, IEntityConfiguration<IamContext>
{
    public override void Map(EntityTypeBuilder<Role> model)
    {
        base.Map(model);

        model.Property(p => p.Name)
            .HasConversion(v => v.Value, v => RoleName.FromPersistence(v));
    }

    public override string GetSchemaName() => SchemaConstants.IAM_SCHEMA_NAME;

    public override string GetTableName() => nameof(Role);
}

public class PagePermissionConfiguration
    : BaseConfiguration<PagePermission>, IEntityConfiguration<IamContext>
{
    public override void Map(EntityTypeBuilder<PagePermission> model)
    {
        base.Map(model);

        model.HasMany(p => p.Actions)
            .WithOne()
            .HasForeignKey(a => a.PagePermissionId);
    }

    public override string GetSchemaName() => SchemaConstants.IAM_SCHEMA_NAME;

    public override string GetTableName() => nameof(PagePermission);
}

public class PageActionConfiguration
    : BaseConfiguration<PageAction>, IEntityConfiguration<IamContext>
{
    public override void Map(EntityTypeBuilder<PageAction> model)
    {
        base.Map(model);
    }

    public override string GetSchemaName() => SchemaConstants.IAM_SCHEMA_NAME;

    public override string GetTableName() => nameof(PageAction);
}