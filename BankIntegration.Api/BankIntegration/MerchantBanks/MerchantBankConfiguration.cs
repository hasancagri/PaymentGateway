namespace PaymentGatewayApi.Modules.BankIntegration.MerchantBanks;

public class MerchantBankConfiguration
    : BaseConfiguration<MerchantBank>, IEntityConfiguration<BankIntegrationContext>
{
    public override void Map(EntityTypeBuilder<MerchantBank> model)
    {
        base.Map(model);
    }

    public override string GetSchemaName() => SchemaConstants.BANK_INTEGRATION_SCHEMA_NAME;
    public override string GetTableName() => nameof(MerchantBank);
}