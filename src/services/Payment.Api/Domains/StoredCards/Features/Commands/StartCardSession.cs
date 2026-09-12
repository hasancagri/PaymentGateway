using System.Globalization;
using Iyz = Payment.Api.Utils;
using Payment.Api.Options;

namespace Payment.Api.Domains.StoredCards.Features.Commands;

/// <summary>
/// 040 US1: Hosted kart-ekleme oturumu başlatır — iyzico Checkout Form initialize (kart-kaydet, nominal
/// doğrulama). PAN mağazaya/gateway'e uğramaz; kullanıcı sağlayıcının hosted formunda girer. Yanıt:
/// tarayıcıda açılacak paymentPageUrl (addUrl) + conversationId. CardSession(Pending) + CF token saklanır
/// (retrieve için). Mevcut userHandle (cardUserKey) verilirse iyzico o kümeye ekler (R2 gruplama).
/// merchantId çağıran token'ın merchant_id claim'inden gelir (İLKE V tenant).
/// </summary>
public static class StartCardSession
{
    // iyzico sandbox nominal-doğrulama fixture'ları — kart-ekleme buyer TAŞIMAZ (gerçek müşteri yok);
    // hosted form ödemesiz açılmaz, minimal tutarla açılır (sandbox'ta para hareketi yok). Config değil,
    // sabit sandbox fixture (nominal no-op). Canlı politika (küçük-doğrula+iade) backlog.
    private const string NominalPrice = "1.0";
    private const string BuyerName = "PG";
    private const string BuyerSurname = "User";
    private const string BuyerIdentity = "11111111111";
    private const string BuyerAddress = "Sandbox";
    private const string BuyerCity = "Istanbul";
    private const string BuyerCountry = "Turkey";
    private const string BuyerIp = "85.34.78.112";
    private const string BuyerGsm = "+905555555555";

    public record StartCardSessionCommand(Guid MerchantId, Guid ConversationId, string CallbackUrl, string? UserHandle);

    public class StartCardSessionResponse
    {
        public string AddUrl { get; set; } = string.Empty;
        public Guid ConversationId { get; set; }
    }

    // --- iyzico Checkout Form initialize wire (slice-sahipli; camelCase JSON) ---

    public class InitializeCheckoutFormRequest
    {
        public string Locale { get; set; } = string.Empty;
        public string ConversationId { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string PaidPrice { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public string BasketId { get; set; } = string.Empty;
        public string PaymentGroup { get; set; } = string.Empty;
        public string CallbackUrl { get; set; } = string.Empty;
        public List<int> EnabledInstallments { get; set; } = [1];
        // Mevcut kullanıcıya ekleme (R2 gruplama) — null/boşsa iyzico yeni cardUserKey üretir.
        public string? CardUserKey { get; set; }
        public CfBuyer Buyer { get; set; } = new();
        public CfAddress ShippingAddress { get; set; } = new();
        public CfAddress BillingAddress { get; set; } = new();
        public List<CfBasketItem> BasketItems { get; set; } = new();
    }

    public class CfBuyer
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Surname { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string GsmNumber { get; set; } = string.Empty;
        public string IdentityNumber { get; set; } = string.Empty;
        public string RegistrationAddress { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
    }

    public class CfAddress
    {
        public string ContactName { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        [Newtonsoft.Json.JsonProperty("address")]
        public string Description { get; set; } = string.Empty;
    }

    public class CfBasketItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category1 { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
    }

    /// <summary>CF initialize yanıtı (wire) — Status/Error Iyz.ProviderResourceV2'den.</summary>
    public class CheckoutFormInitializeResult : Iyz.ProviderResourceV2
    {
        public string Token { get; set; } = string.Empty;
        public string PaymentPageUrl { get; set; } = string.Empty;
        public long TokenExpireTime { get; set; }
    }

    [Transactional]
    public class StartCardSessionCommandHandler
    {
        public async Task<FeatureObjectResultModel<StartCardSessionResponse>> Handle(
            StartCardSessionCommand cmd, IDocumentSession session, Iyz.ProviderOptions providerOptions,
            IyzicoRequestOptions requestOptions, CancellationToken ct)
        {
            var inv = CultureInfo.InvariantCulture;
            var merchantShort = cmd.MerchantId.ToString("N")[..8];
            var email = $"{requestOptions.EmailLocalPrefix}{merchantShort}@{requestOptions.EmailDomain}";

            var request = new InitializeCheckoutFormRequest
            {
                Locale = requestOptions.Locale,
                ConversationId = cmd.ConversationId.ToString("N"),
                Price = NominalPrice,
                PaidPrice = NominalPrice,
                Currency = requestOptions.Currency,
                BasketId = requestOptions.BasketIdPrefix + merchantShort,
                PaymentGroup = requestOptions.PaymentGroup,
                CallbackUrl = cmd.CallbackUrl,
                CardUserKey = string.IsNullOrWhiteSpace(cmd.UserHandle) ? null : cmd.UserHandle,
                Buyer = new CfBuyer
                {
                    Id = requestOptions.BuyerIdPrefix + merchantShort,
                    Name = BuyerName, Surname = BuyerSurname, Email = email, GsmNumber = BuyerGsm,
                    IdentityNumber = BuyerIdentity, RegistrationAddress = BuyerAddress,
                    City = BuyerCity, Country = BuyerCountry, Ip = BuyerIp
                },
                ShippingAddress = NominalAddress(),
                BillingAddress = NominalAddress(),
                BasketItems =
                [
                    new CfBasketItem
                    {
                        Id = requestOptions.BasketItemId, Name = requestOptions.BasketItemName,
                        Category1 = requestOptions.BasketItemCategory, ItemType = requestOptions.ItemType,
                        Price = NominalPrice
                    }
                ]
            };

            CheckoutFormInitializeResult iyzicoResult;
            try
            {
                var uri = providerOptions.BaseUrl + requestOptions.CheckoutFormInitializePath;
                var headers = Iyz.ProviderResourceV2.GetHttpHeadersWithRequestBody(request, uri, providerOptions, request.ConversationId);
                iyzicoResult = await Iyz.RestHttpClientV2.Create().PostAsync<CheckoutFormInitializeResult>(uri, headers, request);
            }
            catch
            {
                return FeatureObjectResultModel<StartCardSessionResponse>.Error(new MessageItem
                { Code = CardVaultResourceConstants.PROVIDER_UNAVAILABLE });
            }

            if (iyzicoResult is null || iyzicoResult.Status != requestOptions.SuccessStatus ||
                string.IsNullOrWhiteSpace(iyzicoResult.Token) || string.IsNullOrWhiteSpace(iyzicoResult.PaymentPageUrl))
                return FeatureObjectResultModel<StartCardSessionResponse>.Error(new MessageItem
                { Code = CardVaultResourceConstants.CARD_SESSION_FAILED });

            // CF token'ı sakla (retrieve için); conversationId = CardSession.Id.
            var cardSession = CardSession.Start(cmd.ConversationId, cmd.MerchantId, iyzicoResult.Token, DateTimeOffset.UtcNow);
            session.Store(cardSession);

            return FeatureObjectResultModel<StartCardSessionResponse>.Ok(new StartCardSessionResponse
            {
                AddUrl = iyzicoResult.PaymentPageUrl,
                ConversationId = cmd.ConversationId
            });

            CfAddress NominalAddress() => new()
            {
                ContactName = $"{BuyerName} {BuyerSurname}", City = BuyerCity, Country = BuyerCountry,
                Description = BuyerAddress
            };
        }
    }
}
