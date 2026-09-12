using Iyz = Payment.Api.Utils;
using Payment.Api.Options;

namespace Payment.Api.Domains.StoredCards.Features.Commands;

/// <summary>
/// 040 US1: Hosted kart-ekleme sonucunu sağlayıcıdan çeker (Checkout Form retrieve) → cardUserKey
/// (=pgUserHandle) + kaydedilen kartın cardToken/gösterim alanları. StoredCard yazılır (per-user
/// cardUserKey — R2 gruplama), CardSession Completed. İptal/hata → CardSession Fail, kalıcı kayıt yok
/// (FR-002). Tek-kullanımlık + süre-sınırlı (R3). merchantId çağıran claim'inden (tenant).
/// </summary>
public static class CompleteCardSession
{
    // Hosted formun geçerli kalacağı süre (store TTL'iyle hizalı, ~15 dk).
    private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(15);

    public record CompleteCardSessionCommand(Guid MerchantId, Guid ConversationId);

    public class CompleteCardSessionResponse
    {
        public string Status { get; set; } = string.Empty; // success / failure
        public string? PgUserHandle { get; set; }
    }

    // --- iyzico Checkout Form retrieve wire (slice-sahipli; camelCase JSON) ---

    public class RetrieveCheckoutFormRequest
    {
        public string Locale { get; set; } = string.Empty;
        public string ConversationId { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }

    /// <summary>CF retrieve yanıtı (wire) — kart-kaydet sonrası cardUserKey/cardToken + gösterim.</summary>
    public class RetrieveCheckoutFormResult : Iyz.ProviderResourceV2
    {
        public string PaymentStatus { get; set; } = string.Empty;
        public string CardUserKey { get; set; } = string.Empty;
        public string CardToken { get; set; } = string.Empty;
        public string BinNumber { get; set; } = string.Empty;
        public string LastFourDigits { get; set; } = string.Empty;
        public string CardAssociation { get; set; } = string.Empty;
    }

    [Transactional]
    public class CompleteCardSessionCommandHandler
    {
        public async Task<FeatureObjectResultModel<CompleteCardSessionResponse>> Handle(
            CompleteCardSessionCommand cmd, IDocumentSession session, Iyz.ProviderOptions providerOptions,
            IyzicoRequestOptions requestOptions, CancellationToken ct)
        {
            var now = DateTimeOffset.UtcNow;
            var cardSession = await session.LoadAsync<CardSession>(cmd.ConversationId, ct);
            if (cardSession is null || cardSession.MerchantId != cmd.MerchantId)
                return FeatureObjectResultModel<CompleteCardSessionResponse>.Error(new MessageItem
                { Code = CardVaultResourceConstants.CARD_NOT_FOUND });

            // Zaten tamamlanmış oturum: idempotent — mevcut sonucu dön.
            if (cardSession.Status == CardSessionStatus.Completed)
                return FeatureObjectResultModel<CompleteCardSessionResponse>.Ok(new CompleteCardSessionResponse
                { Status = "success", PgUserHandle = cardSession.CardUserKey });

            if (!cardSession.IsUsable(now, SessionTtl))
            {
                cardSession.Fail();
                session.Update(cardSession);
                return FeatureObjectResultModel<CompleteCardSessionResponse>.Error(new MessageItem
                { Code = CardVaultResourceConstants.CARD_SESSION_EXPIRED });
            }

            var request = new RetrieveCheckoutFormRequest
            {
                Locale = requestOptions.Locale,
                ConversationId = cmd.ConversationId.ToString("N"),
                Token = cardSession.CheckoutFormToken
            };

            RetrieveCheckoutFormResult iyzicoResult;
            try
            {
                var uri = providerOptions.BaseUrl + requestOptions.CheckoutFormRetrievePath;
                var headers = Iyz.ProviderResourceV2.GetHttpHeadersWithRequestBody(request, uri, providerOptions, request.ConversationId);
                iyzicoResult = await Iyz.RestHttpClientV2.Create().PostAsync<RetrieveCheckoutFormResult>(uri, headers, request);
            }
            catch
            {
                return FeatureObjectResultModel<CompleteCardSessionResponse>.Error(new MessageItem
                { Code = CardVaultResourceConstants.PROVIDER_UNAVAILABLE });
            }

            // Başarısız / kart kaydedilmemiş → oturum Fail, kalıcı kayıt yok (FR-002).
            if (iyzicoResult is null || iyzicoResult.Status != requestOptions.SuccessStatus ||
                string.IsNullOrWhiteSpace(iyzicoResult.CardUserKey) || string.IsNullOrWhiteSpace(iyzicoResult.CardToken))
            {
                cardSession.Fail();
                session.Update(cardSession);
                return FeatureObjectResultModel<CompleteCardSessionResponse>.Ok(new CompleteCardSessionResponse
                { Status = "failure" });
            }

            // Aynı cardToken zaten kayıtlıysa tekrar yazma (idempotent add).
            var existing = await session.Query<StoredCard>()
                .Where(x => x.MerchantId == cmd.MerchantId && x.CardToken == iyzicoResult.CardToken)
                .FirstOrDefaultAsync(ct);
            if (existing is null)
            {
                var created = StoredCard.Create(
                    cmd.MerchantId, iyzicoResult.CardUserKey, iyzicoResult.CardToken,
                    iyzicoResult.BinNumber, iyzicoResult.LastFourDigits,
                    CardAssociationMapper.Map(iyzicoResult.CardAssociation),
                    expiry: string.Empty, // iyzico CF retrieve SKT dönmez (gösterimde boş)
                    holderName: string.Empty);
                if (!created.IsSuccess)
                    return FeatureObjectResultModel<CompleteCardSessionResponse>.Error(created.Messages);
                session.Store(created.Data!);
            }

            var complete = cardSession.Complete(iyzicoResult.CardUserKey);
            if (!complete.IsSuccess)
                return FeatureObjectResultModel<CompleteCardSessionResponse>.Error(complete.Messages);
            session.Update(cardSession);

            return FeatureObjectResultModel<CompleteCardSessionResponse>.Ok(new CompleteCardSessionResponse
            { Status = "success", PgUserHandle = iyzicoResult.CardUserKey });
        }
    }
}
