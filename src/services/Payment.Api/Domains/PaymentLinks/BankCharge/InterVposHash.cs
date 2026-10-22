using System.Security.Cryptography;
using System.Text;

namespace Payment.Api.Domains.PaymentLinks.BankCharge;

/// <summary>
/// Phase-1 (İlke VI/VII): PG-tarafı InterVpos 3D hash (sim `InterVposHash` aynası). İstek hash'i = merchant
/// (PG) üretir, banka doğrular: `SHA1(ShopCode+OrderId+PurchAmount+OkUrl+FailUrl+TxnType+InstallmentCount+
/// Rnd+SecretKey)` base64 (ayraçsız). PAN İÇERMEZ → 3DS'te kart tarayıcıdan bankaya gider, PG sunucusuna
/// uğramaz (SAQ A-EP). Callback hash'i = banka üretir, PG doğrular: değerler ':' + SecretKey → SHA1 → base64.
/// </summary>
public static class InterVposHash
{
    public static string ComputeRequestHash(string shopCode, string orderId, string purchAmount,
        string okUrl, string failUrl, string txnType, string installment, string rnd, string secretKey)
        => Base64Sha1(shopCode + orderId + purchAmount + okUrl + failUrl + txnType + installment + rnd + secretKey);

    public static string ComputeCallbackHash(IEnumerable<string> values, string secretKey)
        => Base64Sha1(string.Join(":", values) + secretKey);

    private static string Base64Sha1(string input)
        => Convert.ToBase64String(SHA1.HashData(Encoding.UTF8.GetBytes(input)));
}
