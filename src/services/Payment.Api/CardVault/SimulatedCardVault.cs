using Payment.Api.Domains.StoredCards;

namespace Payment.Api.CardVault;

/// <summary>
/// <see cref="ICardVault"/> gerçeklemesi — token'ı gerçek <see cref="StoredCard"/> kaydından çözer
/// (017; sabit fixture kalktı). <b>token → StoredCard → BIN → <see cref="CardInfo"/></b>: kayıt
/// yok/Revoked → hata; aksi kartın saklı <see cref="StoredCard.Bin"/>'i 008'in
/// <see cref="ResolveBinCard"/>'ıyla katalogdan çözülür (PAN decrypt EDİLMEZ; yalnız BIN). Tümü
/// server-side; A2A/LLM kanalını geçmez. Merchant eşleşmesi resolve'da YOK (research R3; charge feature'ında).
/// </summary>
public class SimulatedCardVault : ICardVault, IScopedDependency
{
    private readonly IQuerySession _session;

    public SimulatedCardVault(IQuerySession session) => _session = session;

    public async Task<ResultDomain<CardInfo>> ResolveCardInfoAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return ResultDomain<CardInfo>.Error(new MessageItem
            {
                Property = "token",
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
            });
        }

        var card = await _session.LoadAsync<StoredCard>(token, ct);
        if (card is null || card.Status == StoredCardStatus.Revoked)
        {
            return ResultDomain<CardInfo>.Error(new MessageItem
            {
                Property = "token",
                Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
            });
        }

        var cardInfo = await ResolveBinCard.Resolve(_session, card.Bin, ct);
        if (cardInfo is null)
        {
            return ResultDomain<CardInfo>.Error(new MessageItem
            {
                Property = "token",
                Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
            });
        }

        return ResultDomain<CardInfo>.Ok(cardInfo);
    }
}
