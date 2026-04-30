namespace PaymentGatewayApi.Modules.BankIntegration.Banks.Configurations;

public class BankConfiguration
    : BaseConfiguration<Bank>, IEntityConfiguration<BankIntegrationContext>
{
    public override void Map(EntityTypeBuilder<Bank> model)
    {
        base.Map(model);
    }

    public override string GetSchemaName()
    {
        return SchemaConstants.BANK_INTEGRATION_SCHEMA_NAME;
    }


    public override string GetTableName()
    {
        return nameof(Bank);
    }
}