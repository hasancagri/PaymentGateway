namespace PaymentGatewayApi.Modules.BankIntegration.BinRecords.Configurations;

public class BinRecordConfiguration
    : BaseConfiguration<BinRecord>, IEntityConfiguration<BankIntegrationContext>
{
    public override void Map(EntityTypeBuilder<BinRecord> model)
    {
        base.Map(model);
    }

    public override string GetSchemaName() => SchemaConstants.BANK_INTEGRATION_SCHEMA_NAME;

    public override string GetTableName() => nameof(BinRecord);
}