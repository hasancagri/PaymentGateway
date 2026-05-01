using PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.ValueObjects;

namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions;

public class PaymentTransactionConfiguration
    : BaseConfiguration<PaymentTransaction>, IEntityConfiguration<PaymentProcessingContext>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override void Map(EntityTypeBuilder<PaymentTransaction> model)
    {
        base.Map(model);

        model.Property(p => p.OrderId)
            .HasConversion(v => v.Value, v => OrderId.FromPersistence(v));

        model.Property(p => p.Amount)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!);

        model.Property(p => p.CardInfo)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<CardInfo>(v, JsonOptions)!);

        model.Property(p => p.CommissionInfo)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<CommissionInfo>(v, JsonOptions)!);

        model.Property(p => p.RoutingInfo)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<BankRoutingInfo>(v, JsonOptions)!);
    }

    public override string GetSchemaName() => SchemaConstants.PAYMENT_PROCESSING_SCHEMA_NAME;

    public override string GetTableName() => nameof(PaymentTransaction);
}