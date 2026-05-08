using MerchantManagement.Api.Modules.MerchantManagement.Merchants.Entities;
using MerchantManagement.Api.Modules.MerchantManagement.Merchants.ValueObjects;

namespace MerchantManagement.Api.Modules.MerchantManagement.Merchants.Configurations;

public class MerchantConfiguration
    : BaseConfiguration<Merchant>, IEntityConfiguration<MerchantManagementContext>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override void Map(EntityTypeBuilder<Merchant> model)
    {
        base.Map(model);

        model.Property(p => p.Name)
            .HasConversion(v => v.Value, v => MerchantName.FromPersistence(v));

        model.Property(p => p.ContactInfo)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<ContactInfo>(v, JsonOptions)!);

        model.Property(p => p.Address)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<MerchantAddress>(v, JsonOptions)!);

        model.Property(p => p.Mcc)
            .HasConversion(v => v.Value, v => Mcc.FromPersistence(v));

        model.Property(p => p.WebhookUrl)
            .HasConversion(v => v.Value, v => WebhookUrl.FromPersistence(v));
    }

    public override string GetSchemaName() => SchemaConstants.MERCHANT_MANAGEMENT_SCHEMA_NAME;

    public override string GetTableName() => nameof(Merchant);
}

public class ApiKeyConfiguration
    : BaseConfiguration<ApiKey>, IEntityConfiguration<MerchantManagementContext>
{
    public override void Map(EntityTypeBuilder<ApiKey> model)
    {
        base.Map(model);

        model.Property(p => p.KeyValue)
            .HasConversion(v => v.Hash, v => ApiKeyValue.FromHash(v))
            .HasColumnName("KeyHash");
    }

    public override string GetSchemaName() => SchemaConstants.MERCHANT_MANAGEMENT_SCHEMA_NAME;

    public override string GetTableName() => nameof(ApiKey);
}

public class MerchantBankAccountConfiguration
    : BaseConfiguration<MerchantBankAccount>, IEntityConfiguration<MerchantManagementContext>
{
    public override void Map(EntityTypeBuilder<MerchantBankAccount> model)
    {
        base.Map(model);

        model.Property(p => p.Currency)
            .HasConversion(v => v.Code, v => Currency.FromPersistence(v));
    }

    public override string GetSchemaName() => SchemaConstants.MERCHANT_MANAGEMENT_SCHEMA_NAME;

    public override string GetTableName() => nameof(MerchantBankAccount);
}



