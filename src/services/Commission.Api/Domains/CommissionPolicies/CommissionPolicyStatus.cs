namespace Commission.Api.Domains.CommissionPolicies;

/// <summary>
/// Marj politikası statüsü (024). Active → hesaplamada kullanılır; Passive → hesaplama yok sayar
/// (FR-003). Create'te Active doğar.
/// </summary>
public enum CommissionPolicyStatus
{
    Active,
    Passive
}
