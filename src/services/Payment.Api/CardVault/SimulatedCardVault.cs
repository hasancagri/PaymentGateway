
namespace Payment.Api.CardVault;

/// <summary>
/// Geçici <see cref="ICardVault"/> gerçeklemesi — gerçek tokenizasyon/vault feature'ı gelene kadar.
/// Yalnız <b>token → BIN</b> eşlemesini simüle eder (sabit fixture); BIN → <see cref="CardInfo"/>
/// çözümü <b>008'in</b> <see cref="ResolveBinCard"/>'ıyla DB katalogdan yapılır (CP.VPOS BinService
/// DEĞİL). Tümü server-side; A2A/LLM kanalını geçmez. Bilinmeyen/geçersiz token veya katalogda
/// olmayan BIN → hata (kart verisi sızmadan, FR-019).
/// </summary>
public class SimulatedCardVault : ICardVault, IScopedDependency
{
    private readonly IQuerySession _session;

    public SimulatedCardVault(IQuerySession session) => _session = session;

    /// <summary>
    /// Sabit test-token → BIN tablosu (quickstart S1/AC-3). BIN'ler 008 kataloğunda (bincards.json)
    /// GERÇEKTEN bulunur; aksi halde <see cref="ResolveBinCard"/> null döner.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> TokenToBin = new Dictionary<string, string>
    {
        ["tok_credit_taksitli"] = "365770", // 0124 (Bonus) — kredi, taksit destekler
        ["tok_debit"] = "401049"            // 0012 — banka kartı, yalnız peşin (AC-3)
        // Haritada olmayan her token (ör. "tok_invalid") → hata (FR-019).
    };

    public async Task<ResultDomain<CardInfo>> ResolveCardInfoAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || !TokenToBin.TryGetValue(token, out var bin))
        {
            return ResultDomain<CardInfo>.Error(new MessageItem
            {
                Property = "token",
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
            });
        }

        var card = await ResolveBinCard.Resolve(_session, bin, ct);
        if (card is null)
        {
            return ResultDomain<CardInfo>.Error(new MessageItem
            {
                Property = "token",
                Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
            });
        }

        return ResultDomain<CardInfo>.Ok(card);
    }
}