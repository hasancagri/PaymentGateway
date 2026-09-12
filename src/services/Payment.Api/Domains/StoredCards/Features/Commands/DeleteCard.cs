using Iyz = Payment.Api.Utils;
using Payment.Api.Options;

namespace Payment.Api.Domains.StoredCards.Features.Commands;

/// <summary>
/// 040 US4: Kayıtlı kartı sağlayıcıdan siler (userHandle=cardUserKey + cardHandle=cardToken). Sahiplik
/// (FR-012): kart çağıran merchant'ın kümesinde olmalı (yerel StoredCard MerchantId+CardUserKey+CardToken)
/// — aksi CARD_NOT_FOUND (sızıntı yok). Sağlayıcıdan sil (best-effort) + yerel soft-revoke. merchantId
/// çağıran claim'inden.
/// </summary>
public static class DeleteCard
{
    public record DeleteCardCommand(Guid MerchantId, string UserHandle, string CardHandle);

    public class DeleteCardResponse
    {
        public bool Deleted { get; set; }
    }

    /// <summary>iyzico "Saklı Kart sil" istek gövdesi (wire).</summary>
    public class DeleteStoredCardRequest
    {
        public string Locale { get; set; } = string.Empty;
        public string ConversationId { get; set; } = string.Empty;
        public string CardUserKey { get; set; } = string.Empty;
        public string CardToken { get; set; } = string.Empty;
    }

    [Transactional]
    public class DeleteCardCommandHandler
    {
        public async Task<FeatureObjectResultModel<DeleteCardResponse>> Handle(
            DeleteCardCommand cmd, IDocumentSession session, Iyz.ProviderOptions providerOptions,
            IyzicoRequestOptions requestOptions, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.UserHandle) || string.IsNullOrWhiteSpace(cmd.CardHandle))
                return FeatureObjectResultModel<DeleteCardResponse>.Error(new MessageItem
                { Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });

            // Sahiplik (FR-012): kart çağıran merchant'ın kümesinde mi (userHandle + cardHandle eşleşmesi).
            var card = await session.Query<StoredCard>()
                .Where(x => x.MerchantId == cmd.MerchantId && x.CardUserKey == cmd.UserHandle && x.CardToken == cmd.CardHandle)
                .FirstOrDefaultAsync(ct);
            if (card is null)
                return FeatureObjectResultModel<DeleteCardResponse>.Error(new MessageItem
                { Code = CardVaultResourceConstants.CARD_NOT_FOUND });

            // Sağlayıcıdan sil (best-effort) — hata yerel iptali bloklamaz (fail-open).
            try
            {
                var request = new DeleteStoredCardRequest
                {
                    Locale = requestOptions.Locale,
                    ConversationId = requestOptions.ConversationId,
                    CardUserKey = cmd.UserHandle,
                    CardToken = cmd.CardHandle
                };
                var uri = providerOptions.BaseUrl + requestOptions.CardStoragePath;
                var headers = Iyz.ProviderResourceV2.GetHttpHeadersWithRequestBody(request, uri, providerOptions, request.ConversationId);
                await Iyz.RestHttpClientV2.Create().DeleteAsync<Iyz.ProviderResourceV2>(uri, headers, request);
            }
            catch
            {
                // yut: yerel iptal devam eder
            }

            var revoke = card.Revoke();
            if (!revoke.IsSuccess)
                return FeatureObjectResultModel<DeleteCardResponse>.Error(revoke.Messages);
            session.Update(card);

            return FeatureObjectResultModel<DeleteCardResponse>.Ok(new DeleteCardResponse { Deleted = true });
        }
    }
}
