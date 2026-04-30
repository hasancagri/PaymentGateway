namespace PaymentGatewayApi.Modules.IAM.Users.Configurations;

public class UserConfiguration
    : BaseConfiguration<User>, IEntityConfiguration<IamContext>
{
    public override void Map(EntityTypeBuilder<User> model)
    {
        base.Map(model);
    }

    public override string GetSchemaName() => SchemaConstants.IAM_SCHEMA_NAME;

    public override string GetTableName() => nameof(User);
}