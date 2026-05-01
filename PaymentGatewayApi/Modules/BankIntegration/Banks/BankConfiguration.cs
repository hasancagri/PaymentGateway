using PaymentGatewayApi.Modules.BankIntegration.Banks.ValueObjects;

namespace PaymentGatewayApi.Modules.BankIntegration.Banks;

public class BankConfiguration
    : BaseConfiguration<Bank>, IEntityConfiguration<BankIntegrationContext>
{
    public override void Map(EntityTypeBuilder<Bank> model)
    {
        base.Map(model);
        model.Property(p => p.Name)
            .HasConversion(
                text => text.Value,
                value => BankName.FromPersistence(value));
        
        model.Property(p => p.Priority)
            .HasConversion(
                text => text.Value,
                value => BankPriority.FromPersistence(value));
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