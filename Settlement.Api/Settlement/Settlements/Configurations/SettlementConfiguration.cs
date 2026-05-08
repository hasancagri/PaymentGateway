using PaymentGatewayApi.Modules.Settlement.Settlements.Entities;
using PaymentGatewayApi.Modules.Settlement.Settlements.ValueObjects;

namespace PaymentGatewayApi.Modules.Settlement.Settlements.Configurations;

public class SettlementConfiguration
    : BaseConfiguration<Settlement>, IEntityConfiguration<SettlementContext>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override void Map(EntityTypeBuilder<Settlement> model)
    {
        base.Map(model);

        model.Property(p => p.Period)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<SettlementPeriod>(v, JsonOptions)!);

        model.Ignore(p => p.TotalGrossAmount);
        model.Ignore(p => p.TotalCommissionAmount);
        model.Ignore(p => p.TotalNetAmount);
    }

    public override string GetSchemaName() => SchemaConstants.SETTLEMENT_SCHEMA_NAME;

    public override string GetTableName() => nameof(Settlement);
}

public class SettlementLineConfiguration
    : BaseConfiguration<SettlementLine>, IEntityConfiguration<SettlementContext>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override void Map(EntityTypeBuilder<SettlementLine> model)
    {
        base.Map(model);

        model.Property(p => p.GrossAmount)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!);

        model.Property(p => p.CommissionAmount)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!);

        model.Property(p => p.NetAmount)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!);
    }

    public override string GetSchemaName() => SchemaConstants.SETTLEMENT_SCHEMA_NAME;

    public override string GetTableName() => nameof(SettlementLine);
}