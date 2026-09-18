namespace Commission.Api.Domains.CommissionPolicies.Features.Agents.Commands;

// 044 US3: sohbetten marj güncelleme — tam kademe seti alır (kısmi patch yok), tablo bütünüyle
// değişir; hatada mevcut tarife DEĞİŞMEZ (CommissionPolicy.UpdateMargin). Agent slice kendi
// handler'ını taşır (bilinçli tekrar). commission.write kapısı Wolverine middleware'de.
public static class AdminUpdateCommissionMargin
{
    /// <summary>Kademe taşıyıcısı (slice-yerel MCP sözleşmesi); doğrulama MarginTariff.Create'te.</summary>
    public record TierDto(decimal FromAmount, decimal RatePercent, decimal FixedFee);

    [RequiredScope(AuthorizationScopes.CommissionWrite)]
    public record AdminUpdateCommissionMarginCommand(Guid MerchantId, List<TierDto> Tiers);

    public class AdminUpdateCommissionMarginResponse
    {
        public Guid PolicyId { get; set; }
        public Guid MerchantId { get; set; }
        public List<TierDto> Tiers { get; set; } = new();
        public string Status { get; set; } = string.Empty;
    }

    [Transactional]
    public class AdminUpdateCommissionMarginCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminUpdateCommissionMarginResponse>> Handle(
            AdminUpdateCommissionMarginCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var policy = await session.Query<CommissionPolicy>()
                .Where(p => p.MerchantId == cmd.MerchantId && !p.IsDeleted)
                .FirstOrDefaultAsync(ct);
            if (policy is null)
                return FeatureObjectResultModel<AdminUpdateCommissionMarginResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.MerchantId),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var tiers = (cmd.Tiers ?? new List<TierDto>())
                .Select(t => (t.FromAmount, t.RatePercent, t.FixedFee)).ToList();
            var updated = policy.UpdateMargin(tiers);
            if (!updated.IsSuccess)
                return FeatureObjectResultModel<AdminUpdateCommissionMarginResponse>.Error(updated.Messages);

            session.Store(policy);

            return FeatureObjectResultModel<AdminUpdateCommissionMarginResponse>.Ok(
                new AdminUpdateCommissionMarginResponse
                {
                    PolicyId = policy.Id,
                    MerchantId = policy.MerchantId,
                    Tiers = policy.Margin.Tiers
                        .Select(t => new TierDto(t.FromAmount, t.RatePercent, t.FixedFee)).ToList(),
                    Status = policy.Status.ToString()
                });
        }
    }
}

/// <summary>US3 — politikanın marj tarifesini bütünüyle yeni kademe setiyle değiştirir.</summary>
[McpServerToolType]
public static class AdminUpdateCommissionMarginMcpTool
{
    [McpServerTool(Name = Shared.CommissionAdminTools.UpdateMargin)]
    [Description("Merchant'ın komisyon politikasının marj tarifesini günceller. TAM kademe seti " +
                 "gönderilir (kısmi patch yok) — tablo bütünüyle yenisiyle değişir. Politika yoksa " +
                 "kayıt-bulunamadı hatası döner.")]
    public static Task<FeatureObjectResultModel<AdminUpdateCommissionMargin.AdminUpdateCommissionMarginResponse>>
        AdminUpdateCommissionMarginAsync(
            [Description("Politikası güncellenecek merchant kimliği")] Guid merchantId,
            [Description("Yeni TAM kademe seti: fromAmount (TL, ilki 0), ratePercent (ondalık; 0.03 = %3), fixedFee (TL)")]
            List<AdminUpdateCommissionMargin.TierDto> tiers,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminUpdateCommissionMargin.AdminUpdateCommissionMarginResponse>>(
            new AdminUpdateCommissionMargin.AdminUpdateCommissionMarginCommand(merchantId, tiers), ct);
}