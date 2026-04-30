using PaymentGatewayApi.Modules.IAM.Roles.Entities;

namespace PaymentGatewayApi.Modules.IAM.Roles.Configurations;

public class RoleConfiguration
    : BaseConfiguration<Role>, IEntityConfiguration<IamContext>
{
    public override void Map(EntityTypeBuilder<Role> model)
    {
        base.Map(model);
    }

    public override string GetSchemaName() => SchemaConstants.IAM_SCHEMA_NAME;

    public override string GetTableName() => nameof(Role);
}


public class RolePermissionConfiguration
    : BaseConfiguration<RolePermission>, IEntityConfiguration<IamContext>
{
    public override void Map(EntityTypeBuilder<RolePermission> model)
    {
        base.Map(model);
    }

    public override string GetSchemaName() => SchemaConstants.IAM_SCHEMA_NAME;

    public override string GetTableName() => nameof(RolePermission);
}