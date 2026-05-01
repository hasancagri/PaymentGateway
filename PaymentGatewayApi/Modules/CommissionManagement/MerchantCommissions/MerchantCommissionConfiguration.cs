using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.ValueObjects;

namespace PaymentGatewayApi.Modules.CommissionManagement.MerchantCommissions;

public class MerchantCommissionConfiguration
    : BaseConfiguration<MerchantCommission>, IEntityConfiguration<CommissionManagementContext>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override void Map(EntityTypeBuilder<MerchantCommission> model)
    {
        base.Map(model);

        model.Property(p => p.Rate)
            .HasConversion(
                v => v.Value,
                v => CommissionRate.FromPersistence(v));

        model.Property(p => p.Criteria)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<CommissionCriteria>(v, JsonOptions)!);
    }

    public override string GetSchemaName() => SchemaConstants.COMMISSION_MANAGEMENT_SCHEMA_NAME;

    public override string GetTableName() => nameof(MerchantCommission);
}