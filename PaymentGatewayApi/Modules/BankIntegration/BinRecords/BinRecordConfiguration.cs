using PaymentGatewayApi.Modules.BankIntegration.BinRecords.ValueObjects;

namespace PaymentGatewayApi.Modules.BankIntegration.BinRecords;

public class BinRecordConfiguration
    : BaseConfiguration<BinRecord>, IEntityConfiguration<BankIntegrationContext>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override void Map(EntityTypeBuilder<BinRecord> model)
    {
        base.Map(model);

        model.Property(p => p.BinRange)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<BinRange>(v, JsonOptions)!);

        model.Property(p => p.CardInfo)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<BinCardInfo>(v, JsonOptions)!);
    }

    public override string GetSchemaName() => SchemaConstants.BANK_INTEGRATION_SCHEMA_NAME;

    public override string GetTableName() => nameof(BinRecord);
}