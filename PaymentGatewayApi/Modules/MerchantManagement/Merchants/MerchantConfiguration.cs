namespace PaymentGatewayApi.Modules.MerchantManagement.Merchants.Configurations;

public class MerchantConfiguration
    : BaseConfiguration<Merchant>, IEntityConfiguration<MerchantManagementContext>
{
    public override void Map(EntityTypeBuilder<Merchant> model)
    {
        base.Map(model);
    }

    public override string GetSchemaName() => SchemaConstants.MERCHANT_MANAGEMENT_SCHEMA_NAME;

    public override string GetTableName() => nameof(Merchant);
}