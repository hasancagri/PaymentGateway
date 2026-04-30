namespace PaymentGatewayApi.Modules.CommissionManagement.MerchantCommissions.Configurations;

public class MerchantCommissionConfiguration
    : BaseConfiguration<MerchantCommission>, IEntityConfiguration<CommissionManagementContext>
{
    public override void Map(EntityTypeBuilder<MerchantCommission> model)
    {
        base.Map(model);
    }

    public override string GetSchemaName() => SchemaConstants.COMMISSION_MANAGEMENT_SCHEMA_NAME;

    public override string GetTableName() => nameof(MerchantCommission);
}