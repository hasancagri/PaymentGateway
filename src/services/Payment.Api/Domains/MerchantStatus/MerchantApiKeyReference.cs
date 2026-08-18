namespace Payment.Api.Domains.MerchantStatus;

/// <summary>
/// 039: merchant.lifecycle fanout'undan beslenen API-key referansı (010 Reference deseni). Aggregate
/// DEĞİL — davranış taşımaz. Yapısal çekim/retrieve uçlarının X-Api-Key auth'u için: gelen anahtar
/// SHA-256'lanıp <see cref="KeyHash"/> ile aranır → merchant çözülür. Yazan tek yer
/// <see cref="MerchantLifecycleEventHandler"/> (MerchantCreated/Provisioned key taşır), okuyan tek yer
/// ApiKeyAuthenticationHandler. MerchantKey ikili amaç: OAuth ClientSecret + X-Api-Key (demo kararı).
/// </summary>
public class MerchantApiKeyReference
{
    public Guid Id { get; set; }              // = MerchantId
    public string KeyHash { get; set; } = string.Empty; // SHA-256(MerchantKey) hex, kiracı-içi tekil
}
