namespace PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.Configurations;

public class BankCommissionConfiguration
    : BaseConfiguration<BankCommission>, IEntityConfiguration<CommissionManagementContext>
{
    public override void Map(EntityTypeBuilder<BankCommission> model)
    {
        base.Map(model);
    }

    public override string GetSchemaName() => SchemaConstants.COMMISSION_MANAGEMENT_SCHEMA_NAME;

    public override string GetTableName() => nameof(BankCommission);
}