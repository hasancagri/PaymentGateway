using System.Globalization;
using Iyz = Payment.Api.Utils;
using Payment.Api.Options;

namespace Payment.Api.Domains.Payments.Features.Agents;

/// <summary>
/// 038 US1: Agent yüzeyi — kayıtlı kart (vault token) + tutar için iyzico taksit seçenekleri.
/// MCP tool'u (get_installment_options) YALNIZ bu slice'ı çağırır; Commands/Queries'e gitmez (015/016).
/// Wire tipleri slice'a nested (037); Queries/InstallmentOptions'tan bilinçli kopya (kod tekrarı OK).
/// </summary>
public static class InstallmentOptionsForAgent
{
    public record InstallmentOptionsForAgentQuery(Guid MerchantId, string VaultToken, decimal Amount);

    public class InstallmentOptionItem
    {
        public int InstallmentNumber { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class InstallmentOptionsView
    {
        public string Bin { get; set; } = string.Empty;
        public string CardAssociation { get; set; } = string.Empty;
        public List<InstallmentOptionItem> InstallmentDetails { get; set; } = new();
    }

    /// <summary>iyzico "taksit sorgu" istek gövdesi (wire) — bu slice'a ait. camelCase JSON, base tip yok.</summary>
    public class RetrieveInstallmentInfoRequest
    {
        public string Locale { get; set; } = string.Empty;
        public string ConversationId { get; set; } = string.Empty;
        public string BinNumber { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
    }

    /// <summary>iyzico taksit yanıtı (wire) — Status/Error alanları Iyz.ProviderResourceV2'den.</summary>
    public class InstallmentInfoResult : Iyz.ProviderResourceV2
    {
        public List<InstallmentDetail> InstallmentDetails { get; set; } = new();
    }

    public class InstallmentDetail
    {
        public List<InstallmentPrice> InstallmentPrices { get; set; } = new();
    }

    public class InstallmentPrice
    {
        public int? InstallmentNumber { get; set; }
        public string TotalPrice { get; set; } = string.Empty;
    }

    public class InstallmentOptionsForAgentQueryHandler
    {
        public async Task<FeatureObjectResultModel<InstallmentOptionsView>> Handle(
            InstallmentOptionsForAgentQuery query, IDocumentSession session,
            Iyz.ProviderOptions providerOptions, IyzicoRequestOptions requestOptions, CancellationToken ct)
        {
            // Vault token → StoredCard: kiracı sınırı + Active (Revoked/yabancı reddi).
            var card = await session.LoadAsync<StoredCard>(query.VaultToken, ct);
            if (card is null || card.MerchantId != query.MerchantId)
                return FeatureObjectResultModel<InstallmentOptionsView>.Error(new MessageItem
                { Property = nameof(query.VaultToken), Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND });
            if (card.Status != StoredCardStatus.Active)
                return FeatureObjectResultModel<InstallmentOptionsView>.Error(new MessageItem
                { Property = nameof(query.VaultToken), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });

            InstallmentInfoResult info;
            try
            {
                var request = new RetrieveInstallmentInfoRequest
                {
                    Locale = requestOptions.Locale,
                    ConversationId = requestOptions.ConversationId,
                    BinNumber = card.Bin,
                    Price = query.Amount.ToString(CultureInfo.InvariantCulture)
                };
                var uri = providerOptions.BaseUrl + requestOptions.InstallmentPath;
                var headers = Iyz.ProviderResourceV2.GetHttpHeadersWithRequestBody(request, uri, providerOptions, request.ConversationId);
                info = await Iyz.RestHttpClientV2.Create().PostAsync<InstallmentInfoResult>(uri, headers, request);
            }
            catch
            {
                return FeatureObjectResultModel<InstallmentOptionsView>.Error(new MessageItem
                { Property = nameof(query.VaultToken), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });
            }

            if (info is null || info.Status != requestOptions.SuccessStatus)
                return FeatureObjectResultModel<InstallmentOptionsView>.Error(new MessageItem
                { Property = nameof(query.VaultToken), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });

            var items = new List<InstallmentOptionItem>();
            foreach (var detail in info.InstallmentDetails ?? new())
            foreach (var price in detail.InstallmentPrices ?? new())
            {
                if (price.InstallmentNumber is { } n &&
                    decimal.TryParse(price.TotalPrice, NumberStyles.Number, CultureInfo.InvariantCulture, out var total))
                    items.Add(new InstallmentOptionItem { InstallmentNumber = n, TotalPrice = total });
            }

            return FeatureObjectResultModel<InstallmentOptionsView>.Ok(new InstallmentOptionsView
            {
                Bin = card.Bin,
                CardAssociation = card.Brand.ToString(),
                InstallmentDetails = items.OrderBy(i => i.InstallmentNumber).ToList()
            });
        }
    }
}