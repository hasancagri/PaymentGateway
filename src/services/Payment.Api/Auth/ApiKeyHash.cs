using System.Security.Cryptography;
using System.Text;

namespace Payment.Api.Auth;

/// <summary>
/// 039: X-Api-Key → deterministik SHA-256 hex. Deterministik (salt YOK) — anahtar yüksek-entropili
/// bir sır olduğundan (parola değil) lookup için salt gerekmez. Yazan (lifecycle handler) ile okuyan
/// (auth handler) BİREBİR aynı hash'i üretmeli → tek yer (correctness; abstraction değil).
/// </summary>
public static class ApiKeyHash
{
    public static string Compute(string apiKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey.Trim()));
        return Convert.ToHexString(bytes); // uppercase hex, deterministik
    }
}
