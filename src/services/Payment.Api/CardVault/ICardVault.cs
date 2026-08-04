namespace Payment.Api.CardVault;

/// <summary>
/// Kart kasası seam'i: kayıtlı kart token'ını güvenli tarafta kart bilgisine (<see cref="CardInfo"/>:
/// BIN düzeyi — banka kodu, kredi/banka, taksit destekleyen bankalar) çözer. 007'de yalnız BIN
/// gerekir; tam PAN çekim feature'ında. Çözüm A2A/LLM kanalını GEÇMEZ (server-side, FR-006).
/// Gerçek tokenizasyon/vault ayrı feature; 007 bu yeteneği yalnız TÜKETİR.
/// </summary>
public interface ICardVault
{
    Task<ResultDomain<CardInfo>> ResolveCardInfoAsync(string token, CancellationToken ct = default);
}