using Iyz = Payment.Api.Utils;
using Payment.Api.Options;

namespace Payment.Api.Domains.HostedPayments.Features.Commands;

/// <summary>
/// 041 US1: Store için hosted ödeme başlatır — iyzico Checkout Form initialize. Kart bilgisi store/PG'ye
/// girmez; müşteri iyzico hosted formunda öder. Yanıt: HostedUrl (paymentPageUrl) + PgPaymentRef.
/// Charge yalnız Active merchant (İlke V, fail-closed). Aynı (merchant, OrderRef) tekil (FR-004).
/// callbackUrl = PG-sahipli base + per-session CallbackToken (C1 secret-token kapısı). merchantId
/// çağıran X-Api-Key claim'inden (İlke V tenant). Buyer/basket sentetik (store bunları göndermez; sandbox).
/// </summary>
public static class InitiateHostedPayment
{
    // iyzico sandbox sentetik buyer/adres — store→PG kontratı buyer TAŞIMAZ (038/040 sentez deseni);
    // hosted form buyer/address zorunlu kılar. Sabit sandbox fixture (config değil — sandbox no-op).
    private const string BuyerName = "PG";
    private const string BuyerSurname = "Store";
    private const string BuyerIdentity = "11111111111";
    private const string BuyerAddress = "Sandbox";
    private const string BuyerCity = "Istanbul";
    private const string BuyerCountry = "Turkey";
    private const string BuyerIp = "85.34.78.112";
    private const string BuyerGsm = "+905555555555";

    public record InitiateHostedPaymentCommand(
        Guid MerchantId, decimal Amount, string Currency, string OrderRef, string CallbackUrl);

    public class InitiateHostedPaymentResponse
    {
        public string HostedUrl { get; set; } = string.Empty;
        public string PgPaymentRef { get; set; } = string.Empty;
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
        public CfBuyer Buyer { get; set; } = new();
        public CfAddress ShippingAddress { get; set; } = new();
        public CfAddress BillingAddress { get; set; } = new();
        public List<CfBasketItem> BasketItems { get; set; } = [];
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
        [JsonProperty("address")]
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

    public class CheckoutFormInitializeResult : Iyz.ProviderResourceV2
    {
        public string Token { get; set; } = string.Empty;
        public string PaymentPageUrl { get; set; } = string.Empty;
        public long TokenExpireTime { get; set; }
    }

    private const string TryCurrency = "TRY";
    private const string ActiveStatus = "Active";

    [Transactional]
    public class InitiateHostedPaymentCommandHandler
    {
        public async Task<FeatureObjectResultModel<InitiateHostedPaymentResponse>> Handle(
            InitiateHostedPaymentCommand cmd, IDocumentSession session, Iyz.ProviderOptions providerOptions,
            IyzicoRequestOptions requestOptions, HostedPaymentOptions hostedOptions, CancellationToken ct)
        {
            // Alan doğrulama: yalnız TRY (FR-012); tutar > 0.
            if (!string.Equals(cmd.Currency, TryCurrency, StringComparison.OrdinalIgnoreCase))
                return FeatureObjectResultModel<InitiateHostedPaymentResponse>.Error(new MessageItem
                { Code = HostedPaymentResourceConstants.UNSUPPORTED_CURRENCY });
            if (cmd.Amount <= 0 || string.IsNullOrWhiteSpace(cmd.OrderRef) || string.IsNullOrWhiteSpace(cmd.CallbackUrl))
                return FeatureObjectResultModel<InitiateHostedPaymentResponse>.Error(new MessageItem
                { Code = HostedPaymentResourceConstants.INVALID_REQUEST });

            // Statü kapısı: referans yok veya Active değil → fail-closed RET (sağlayıcıya gidilmez, İlke V).
            var merchantStatus = await session.LoadAsync<MerchantStatusReference>(cmd.MerchantId, ct);
            if (merchantStatus is null ||
                !string.Equals(merchantStatus.Status, ActiveStatus, StringComparison.OrdinalIgnoreCase))
                return FeatureObjectResultModel<InitiateHostedPaymentResponse>.Error(new MessageItem
                { Code = HostedPaymentResourceConstants.MERCHANT_NOT_ACTIVE });

            // İdempotent başlatma (FR-004): aynı (merchant, OrderRef) Pending girişim varsa yeni AÇMA.
            var existing = await session.Query<HostedPaymentSession>()
                .FirstOrDefaultAsync(x => x.MerchantId == cmd.MerchantId && x.OrderRef == cmd.OrderRef, ct);
            if (existing is not null && existing.Status == HostedPaymentStatus.Pending &&
                !string.IsNullOrWhiteSpace(existing.HostedUrl))
                return FeatureObjectResultModel<InitiateHostedPaymentResponse>.Ok(new InitiateHostedPaymentResponse
                { HostedUrl = existing.HostedUrl, PgPaymentRef = existing.Id.ToString("N") });

            // Yeni girişim + per-session secret callbackToken (C1).
            var callbackToken = Guid.NewGuid().ToString("N");
            var startResult = HostedPaymentSession.Start(cmd.MerchantId, cmd.OrderRef, cmd.Amount, cmd.CallbackUrl, callbackToken);
            if (!startResult.IsSuccess)
                return FeatureObjectResultModel<InitiateHostedPaymentResponse>.Error(startResult.Messages);
            var hostedSession = startResult.Data!;

            var price = cmd.Amount.ToString(CultureInfo.InvariantCulture);
            var merchantShort = cmd.MerchantId.ToString("N")[..8];
            var email = $"{requestOptions.EmailLocalPrefix}{merchantShort}@{requestOptions.EmailDomain}";
            var conversationId = hostedSession.Id.ToString("N"); // I1: PgPaymentRef (OrderRef değil)

            var request = new InitializeCheckoutFormRequest
            {
                Locale = requestOptions.Locale,
                ConversationId = conversationId,
                Price = price,
                PaidPrice = price,
                Currency = requestOptions.Currency,
                BasketId = requestOptions.BasketIdPrefix + merchantShort,
                PaymentGroup = requestOptions.PaymentGroup,
                // C1: iyzico callback = PG base + secret token; bilinmeyen token uca giremez.
                CallbackUrl = $"{hostedOptions.IyzicoCallbackBaseUrl}/{callbackToken}",
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
                        Price = price
                    }
                ]
            };

            CheckoutFormInitializeResult iyzicoResult;
            try
            {
                var uri = providerOptions.BaseUrl + requestOptions.CheckoutFormInitializePath;
                var headers = Iyz.ProviderResourceV2.GetHttpHeadersWithRequestBody(request, uri, providerOptions, conversationId);
                iyzicoResult = await Iyz.RestHttpClientV2.Create().PostAsync<CheckoutFormInitializeResult>(uri, headers, request);
            }
            catch
            {
                return FeatureObjectResultModel<InitiateHostedPaymentResponse>.Error(new MessageItem
                { Code = HostedPaymentResourceConstants.PROVIDER_UNAVAILABLE });
            }

            if (iyzicoResult is null || iyzicoResult.Status != requestOptions.SuccessStatus ||
                string.IsNullOrWhiteSpace(iyzicoResult.Token) || string.IsNullOrWhiteSpace(iyzicoResult.PaymentPageUrl))
                return FeatureObjectResultModel<InitiateHostedPaymentResponse>.Error(new MessageItem
                { Code = HostedPaymentResourceConstants.INITIALIZE_FAILED });

            var attach = hostedSession.AttachCheckoutForm(iyzicoResult.Token, iyzicoResult.PaymentPageUrl);
            if (!attach.IsSuccess)
                return FeatureObjectResultModel<InitiateHostedPaymentResponse>.Error(attach.Messages);
            session.Store(hostedSession);

            return FeatureObjectResultModel<InitiateHostedPaymentResponse>.Ok(new InitiateHostedPaymentResponse
            {
                HostedUrl = iyzicoResult.PaymentPageUrl,
                PgPaymentRef = hostedSession.Id.ToString("N")
            });

            CfAddress NominalAddress() => new()
            {
                ContactName = $"{BuyerName} {BuyerSurname}", City = BuyerCity, Country = BuyerCountry,
                Description = BuyerAddress
            };
        }
    }
}
