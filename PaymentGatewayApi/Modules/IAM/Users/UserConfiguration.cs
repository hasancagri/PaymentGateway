using PaymentGatewayApi.Modules.IAM.Users.Entities;
using PaymentGatewayApi.Modules.IAM.Users.ValueObjects;

namespace PaymentGatewayApi.Modules.IAM.Users;

public class UserConfiguration
    : BaseConfiguration<User>, IEntityConfiguration<IamContext>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override void Map(EntityTypeBuilder<User> model)
    {
        base.Map(model);

        model.Property(p => p.Email)
            .HasConversion(v => v.Value, v => Email.FromPersistence(v));

        model.Property(p => p.Password)
            .HasConversion(v => v.Hash, v => PasswordHash.FromHash(v))
            .HasColumnName("PasswordHash");

        model.Property(p => p.FullName)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<FullName>(v, JsonOptions)!);
    }

    public override string GetSchemaName() => SchemaConstants.IAM_SCHEMA_NAME;

    public override string GetTableName() => nameof(User);
}

public class UserRoleConfiguration
    : BaseConfiguration<UserRole>, IEntityConfiguration<IamContext>
{
    public override void Map(EntityTypeBuilder<UserRole> model)
    {
        base.Map(model);
    }

    public override string GetSchemaName() => SchemaConstants.IAM_SCHEMA_NAME;

    public override string GetTableName() => nameof(UserRole);
}