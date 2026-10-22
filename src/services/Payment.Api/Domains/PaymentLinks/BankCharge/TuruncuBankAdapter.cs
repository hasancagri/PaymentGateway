using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace Payment.Api.Domains.PaymentLinks.BankCharge;

/// <summary>
/// Phase-1 (İlke VII — aynı port, FARKLI tel): TuruncuBank adapter'ı. MaviBank/SarıBank InterVpos
/// (form-urlencoded + `;;` + SHA1); TuruncuBank **XML istek/cevap + SHA512** `{BaseUrl}/turuncubank/pos`.
/// Aynı `IBankPaymentAdapter` portu; wire bu adapter'a özel nested (anti-corruption). PAN tele geçer,
/// saklanmaz/loglanmaz (İlke VI). Durum-sorgu TuruncuBank'ta yok (status ucu seam) → NOTCHARGED.
/// </summary>
public sealed class TuruncuBankAdapter : IBankPaymentAdapter
{
    private const string PaymentPath = "/turuncubank/pos";
    private const string TryCurrencyNumeric = "949";

    private static readonly HttpClient Client = new();
    private readonly InterVposBankProfile _profile;

    public TuruncuBankAdapter(InterVposBankProfile profile) => _profile = profile;

    public string BankCode => _profile.BankCode;

    public async Task<ResultDomain<BankChargeOutcome>> ChargeAsync(BankChargeRequest request, CancellationToken ct)
    {
        var orderId = $"{request.OrderRef}-{Guid.NewGuid():N}"[..Math.Min(request.OrderRef.Length + 9, 40)];
        var amount = request.Amount.ToString(CultureInfo.InvariantCulture);
        var hash = Sha512(_profile.ShopCode + orderId + amount + _profile.SecretKey);

        var xml = new XElement("PaymentRequest",
            new XElement("ShopCode", _profile.ShopCode),
            new XElement("UserCode", _profile.UserCode),
            new XElement("UserPass", _profile.UserPass),
            new XElement("Pan", request.Card.Pan),
            new XElement("Expiry", FormatExpiry(request.Card.ExpiryMonth, request.Card.ExpiryYear)),
            new XElement("Cvv", request.Card.Cvv),
            new XElement("Amount", amount),
            new XElement("Currency", MapCurrency(request.Currency)),
            new XElement("OrderId", orderId),
            new XElement("Hash", hash));

        string body;
        try
        {
            using var content = new StringContent(xml.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "application/xml");
            var response = await Client.PostAsync(_profile.BaseUrl.TrimEnd('/') + PaymentPath, content, ct);
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception)
        {
            // Ağ hatası = belirsiz; durum-sorgu yok (seam) → teknik (reconcile INDOUBT ile devralır).
            return ResultDomain<BankChargeOutcome>.Ok(
                BankChargeOutcome.Declined(DeclineClass.TechnicalFailure, "INDOUBT", orderId));
        }

        return ResultDomain<BankChargeOutcome>.Ok(TuruncuResponseParser.Parse(body) with { BankOrderId = orderId });
    }

    // TuruncuBank durum-sorgu/void/refund ucu yok (bu versiyonda seam) → desteklenmez.
    public Task<ResultDomain<BankChargeOutcome>> QueryStatusAsync(string orderId, CancellationToken ct)
        => Task.FromResult(ResultDomain<BankChargeOutcome>.Ok(
            BankChargeOutcome.Declined(DeclineClass.TechnicalFailure, "NOTCHARGED")));

    public Task<ResultDomain<BankChargeOutcome>> VoidAsync(string bankOrderId, CancellationToken ct)
        => Task.FromResult(ResultDomain<BankChargeOutcome>.Ok(
            BankChargeOutcome.Declined(DeclineClass.ConfigError, "NOTSUPPORTED")));

    public Task<ResultDomain<BankChargeOutcome>> RefundAsync(string bankOrderId, decimal amount, CancellationToken ct)
        => Task.FromResult(ResultDomain<BankChargeOutcome>.Ok(
            BankChargeOutcome.Declined(DeclineClass.ConfigError, "NOTSUPPORTED")));

    private static string FormatExpiry(string month, string year)
    {
        var mm = month.Trim().PadLeft(2, '0');
        var y = year.Trim();
        return mm + (y.Length >= 2 ? y[^2..] : y.PadLeft(2, '0'));
    }

    private static string MapCurrency(string currency)
        => string.Equals(currency, "TRY", StringComparison.OrdinalIgnoreCase) ? TryCurrencyNumeric : currency;

    private static string Sha512(string input)
        => Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes(input)));
}

/// <summary>
/// Anti-corruption (saf, test-first): TuruncuBank XML cevabı → normalize BankChargeOutcome. ResultCode "00"
/// onay; diğer kodlar red sınıfına (InterVpos ResponseMapper aynası, farklı tel).
/// </summary>
public static class TuruncuResponseParser
{
    public static BankChargeOutcome Parse(string body)
    {
        XElement? r;
        try { r = XDocument.Parse(body).Root; }
        catch { return BankChargeOutcome.Declined(DeclineClass.IssuerDecline, "UNKNOWN"); }

        string Get(string n) => r?.Element(n)?.Value ?? string.Empty;
        var code = Get("ResultCode");

        if (code == "00")
            return BankChargeOutcome.Approved(Get("TransId"), Get("AuthCode"), code);

        var declineClass = code switch
        {
            "51" => DeclineClass.IssuerDecline,
            "05" => DeclineClass.IssuerDecline,
            "59" => DeclineClass.FraudDecline,
            "91" => DeclineClass.TechnicalFailure,
            "99" => DeclineClass.ConfigError,
            _ => DeclineClass.IssuerDecline
        };
        return BankChargeOutcome.Declined(declineClass, string.IsNullOrEmpty(code) ? "UNKNOWN" : code, Get("TransId"));
    }
}
