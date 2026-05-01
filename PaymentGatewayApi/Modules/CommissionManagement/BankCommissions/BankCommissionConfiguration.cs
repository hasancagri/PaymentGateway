using System.Text.Json;
using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.ValueObjects;

namespace PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.Configurations;

public class BankCommissionConfiguration
    : BaseConfiguration<BankCommission>, IEntityConfiguration<CommissionManagementContext>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override void Map(EntityTypeBuilder<BankCommission> model)
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

    public override string GetTableName() => nameof(BankCommission);
}