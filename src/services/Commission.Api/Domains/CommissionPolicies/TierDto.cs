namespace Commission.Api.Domains.CommissionPolicies;

/// <summary>
/// Kademe taşıyıcısı (030) — Create/UpdateMargin gövdeleri ve List/Get yanıtları bu şekli kullanır
/// (contracts/api.md). Aggregate köküne ait sözleşme tipi (mapping istisnası); doğrulama
/// <see cref="ValueObjects.MarginTariff.Create"/>'tedir.
/// </summary>
public record TierDto(decimal FromAmount, decimal RatePercent, decimal FixedFee);
