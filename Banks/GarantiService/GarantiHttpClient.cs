using System.Text;
using System.Xml;
using System.Xml.Serialization;
using GarantiService.Models;

namespace GarantiService;

public class GarantiHttpClient
{
    private static readonly Dictionary<string, string> CurrencyCodes = new()
    {
        ["TRY"] = "949",
        ["USD"] = "840",
        ["EUR"] = "978",
        ["GBP"] = "826",
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GarantiHttpClient> _logger;

    public GarantiHttpClient(IHttpClientFactory httpClientFactory, ILogger<GarantiHttpClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<GarantiAuthResponse?> AuthAsync(
        string cardNo, string expiryMonth, string expiryYear, string cvv2,
        string amount, string currency, string cardHolderName, string cardHolderIp,
        int installmentCount, string orderId,
        string merchantCode, string terminalCode,
        CancellationToken ct)
    {
        var amountFormatted = ((long)(decimal.Parse(amount) * 100)).ToString();
        var expireDate = $"{expiryMonth}{expiryYear[^2..]}";
        var currencyCode = CurrencyCodes.GetValueOrDefault(currency, "949");
        var installment = installmentCount > 1 ? installmentCount.ToString() : string.Empty;

        var hashData = ComputeHash(terminalCode, orderId, amountFormatted, currencyCode);

        var request = new GarantiAuthRequest
        {
            Terminal = new GarantiTerminal
            {
                TerminalId = terminalCode,
                MerchantId = merchantCode,
                HashData = hashData
            },
            Customer = new GarantiCustomer { IpAddress = cardHolderIp },
            Card = new GarantiCard
            {
                Number = cardNo,
                ExpireDate = expireDate,
                Cvv2 = cvv2
            },
            Order = new GarantiOrder { OrderId = orderId },
            Transaction = new GarantiTransaction
            {
                Amount = amountFormatted,
                CurrencyCode = currencyCode,
                InstallmentCnt = installment
            }
        };

        var xmlBody = Serialize(request);
        var client = _httpClientFactory.CreateClient("Garanti");

        var httpResponse = await client.PostAsync(
            string.Empty,
            new StringContent(xmlBody, Encoding.UTF8, "text/xml"),
            ct);

        httpResponse.EnsureSuccessStatusCode();

        var responseXml = await httpResponse.Content.ReadAsStringAsync(ct);
        return Deserialize<GarantiAuthResponse>(responseXml);
    }

    // SHA1( TerminalCode + OrderId + Amount + CurrencyCode )
    private static string ComputeHash(string terminalCode, string orderId, string amount, string currencyCode)
    {
        var raw = $"{terminalCode}{orderId}{amount}{currencyCode}";
        var hashBytes = System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hashBytes).ToUpperInvariant();
    }

    private static string Serialize<T>(T obj)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var writer = new StringWriter();
        using var xmlWriter = XmlWriter.Create(writer, new XmlWriterSettings { Indent = false });
        serializer.Serialize(xmlWriter, obj);
        return writer.ToString();
    }

    private static T Deserialize<T>(string xml)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StringReader(xml);
        return (T)serializer.Deserialize(reader)!;
    }
}