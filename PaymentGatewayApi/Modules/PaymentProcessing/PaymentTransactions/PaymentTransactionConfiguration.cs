namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Configurations;

public class PaymentTransactionConfiguration
    : BaseConfiguration<PaymentTransaction>, IEntityConfiguration<PaymentProcessingContext>
{
    public override void Map(EntityTypeBuilder<PaymentTransaction> model)
    {
        base.Map(model);
    }

    public override string GetSchemaName() => SchemaConstants.PAYMENT_PROCESSING_SCHEMA_NAME;

    public override string GetTableName() => nameof(PaymentTransaction);
}