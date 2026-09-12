using Iyz = Payment.Api.Utils;
using Payment.Api.Options;

namespace Payment.Api.Domains.StoredCards.Features.Queries;

/// <summary>
/// 040 US2: Kullanıcının kayıtlı kartlarını sağlayıcıdan CANLI listeler (userHandle=cardUserKey). Gösterim
/// alanları + opak cardHandle (=cardToken); PAN/CVV asla. Tenant (FR-012): userHandle çağıran merchant'a
/// ait olmalı (yerel StoredCard varlığıyla doğrulanır) — aksi boş liste (sızıntı yok). SKT sağlayıcı
/// listesinde yok → yerel StoredCard'dan zenginleştirilir (varsa). Sağlayıcı erişilemez → PROVIDER_UNAVAILABLE.
/// </summary>
public static class ListCards
{
    public record ListCardsQuery(Guid MerchantId, string UserHandle);

    public class CardView
    {
        public string CardHandle { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Last4 { get; set; } = string.Empty;
        public int ExpiryMonth { get; set; }
        public int ExpiryYear { get; set; }
        public string? Alias { get; set; }
    }

    // --- iyzico Card list wire (slice-sahipli; camelCase JSON) ---

    public class RetrieveCardListRequest
    {
        public string Locale { get; set; } = string.Empty;
        public string ConversationId { get; set; } = string.Empty;
        public string CardUserKey { get; set; } = string.Empty;
    }

    public class CardListResult : Iyz.ProviderResourceV2
    {
        public string CardUserKey { get; set; } = string.Empty;
        public List<CardDetail> CardDetails { get; set; } = new();
    }

    public class CardDetail
    {
        public string CardToken { get; set; } = string.Empty;
        public string CardAlias { get; set; } = string.Empty;
        public string BinNumber { get; set; } = string.Empty;
        public string LastFourDigits { get; set; } = string.Empty;
        public string CardAssociation { get; set; } = string.Empty;
    }

    public class ListCardsQueryHandler
    {
        public async Task<FeatureListResultModel<CardView>> Handle(
            ListCardsQuery query, IQuerySession session, Iyz.ProviderOptions providerOptions,
            IyzicoRequestOptions requestOptions, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(query.UserHandle))
                return FeatureListResultModel<CardView>.Ok(new List<CardView>());

            // Tenant (FR-012): bu merchant bu cardUserKey için hiç kart oluşturmuşsa yerel iz vardır.
            // Yoksa yabancı/bilinmeyen handle → boş liste (sızıntı yok).
            var localCards = await session.Query<StoredCard>()
                .Where(x => x.MerchantId == query.MerchantId && x.CardUserKey == query.UserHandle)
                .ToListAsync(ct);
            if (localCards.Count == 0)
                return FeatureListResultModel<CardView>.Ok(new List<CardView>());

            var request = new RetrieveCardListRequest
            {
                Locale = requestOptions.Locale,
                ConversationId = requestOptions.ConversationId,
                CardUserKey = query.UserHandle
            };

            CardListResult iyzicoResult;
            try
            {
                var uri = providerOptions.BaseUrl + requestOptions.CardListPath;
                var headers = Iyz.ProviderResourceV2.GetHttpHeadersWithRequestBody(request, uri, providerOptions, request.ConversationId);
                iyzicoResult = await Iyz.RestHttpClientV2.Create().PostAsync<CardListResult>(uri, headers, request);
            }
            catch
            {
                return FeatureListResultModel<CardView>.Error(new MessageItem
                { Code = CardVaultResourceConstants.PROVIDER_UNAVAILABLE });
            }

            if (iyzicoResult is null || iyzicoResult.Status != requestOptions.SuccessStatus)
                return FeatureListResultModel<CardView>.Error(new MessageItem
                { Code = CardVaultResourceConstants.PROVIDER_UNAVAILABLE });

            // Yerel iz SKT zenginleştirmesi (sağlayıcı listede SKT dönmez).
            var byToken = localCards.ToDictionary(c => c.CardToken, c => c);
            var views = iyzicoResult.CardDetails.Select(d =>
            {
                var (m, y) = ParseExpiry(byToken.TryGetValue(d.CardToken, out var lc) ? lc.Expiry : string.Empty);
                return new CardView
                {
                    CardHandle = d.CardToken,
                    Brand = CardAssociationMapper.Map(d.CardAssociation).ToString(),
                    Last4 = d.LastFourDigits,
                    ExpiryMonth = m,
                    ExpiryYear = y,
                    Alias = d.CardAlias
                };
            }).ToList();

            return FeatureListResultModel<CardView>.Ok(views);
        }

        // "MM/yy" → (month, 20yy); parse edilemezse (0,0).
        private static (int Month, int Year) ParseExpiry(string expiry)
        {
            var parts = (expiry ?? string.Empty).Split('/');
            if (parts.Length != 2) return (0, 0);
            if (!int.TryParse(parts[0].Trim(), out var m)) return (0, 0);
            if (!int.TryParse(parts[1].Trim(), out var y)) return (m, 0);
            return (m, y < 100 ? 2000 + y : y);
        }
    }
}
