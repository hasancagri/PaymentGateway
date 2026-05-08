using PaymentGatewayApi.Modules.Settlement.MerchantBalances.Entities;

namespace PaymentGatewayApi.Modules.Settlement.MerchantBalances;

public class MerchantBalanceConfiguration
    : BaseConfiguration<MerchantBalance>, IEntityConfiguration<SettlementContext>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override void Map(EntityTypeBuilder<MerchantBalance> model)
    {
        base.Map(model);

        model.Property(p => p.Balance)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!);
    }

    public override string GetSchemaName() => SchemaConstants.SETTLEMENT_SCHEMA_NAME;

    public override string GetTableName() => nameof(MerchantBalance);
}

public class BalanceMovementConfiguration
    : BaseConfiguration<BalanceMovement>, IEntityConfiguration<SettlementContext>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override void Map(EntityTypeBuilder<BalanceMovement> model)
    {
        base.Map(model);

        model.Property(p => p.Amount)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!);
    }

    public override string GetSchemaName() => SchemaConstants.SETTLEMENT_SCHEMA_NAME;

    public override string GetTableName() => nameof(BalanceMovement);
}

public class WithdrawalRequestConfiguration
    : BaseConfiguration<WithdrawalRequest>, IEntityConfiguration<SettlementContext>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override void Map(EntityTypeBuilder<WithdrawalRequest> model)
    {
        base.Map(model);

        model.Property(p => p.Amount)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!);
    }

    public override string GetSchemaName() => SchemaConstants.SETTLEMENT_SCHEMA_NAME;

    public override string GetTableName() => nameof(WithdrawalRequest);
}