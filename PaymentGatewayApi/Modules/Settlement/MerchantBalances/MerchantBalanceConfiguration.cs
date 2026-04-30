using PaymentGatewayApi.Contexts.Settlement;

namespace PaymentGatewayApi.Modules.Settlement.MerchantBalances.Configurations;

public class MerchantBalanceConfiguration
    : BaseConfiguration<MerchantBalance>, IEntityConfiguration<SettlementContext>
{
    public override void Map(EntityTypeBuilder<MerchantBalance> model)
    {
        base.Map(model);
    }

    public override string GetSchemaName() => SchemaConstants.SETTLEMENT_SCHEMA_NAME;

    public override string GetTableName() => nameof(MerchantBalance);
}