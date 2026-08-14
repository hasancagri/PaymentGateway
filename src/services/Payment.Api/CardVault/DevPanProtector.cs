using System.Security.Cryptography;
using System.Text;
using Common.Dependencies;

namespace Payment.Api.CardVault;

/// <summary>
/// Dev/simüle <see cref="IPanProtector"/>: sabit anahtarlı AES ile reversible enc-at-rest. Amaç ham
/// PAN'ın DB'de düz durmamasını modellemek (sınır davranışı doğru), gerçek gizlilik değil —
/// prod'da KMS/HSM ile değişir (kapsam dışı). Anahtar dev-sabit (config değil): dev-only, prod'a çıkmaz.
/// </summary>
public sealed class DevPanProtector : IPanProtector, ISingletonDependency
{
    // Dev-sabit 256-bit anahtar (16 byte IV base'e prepend edilir). Prod'da HSM/KMS ile değişir.
    private static readonly byte[] Key =
        SHA256.HashData(Encoding.UTF8.GetBytes("dev-pan-protector-key-017-card-vault"));

    public string Protect(string pan)
    {
        using var aes = Aes.Create();
        aes.Key = Key;
        aes.GenerateIV();

        var plain = Encoding.UTF8.GetBytes(pan);
        var cipher = aes.EncryptCbc(plain, aes.IV);

        // IV'yi cipher önüne ekle → tek base64 string (Reveal ileride IV'yi ayırır).
        var combined = new byte[aes.IV.Length + cipher.Length];
        Buffer.BlockCopy(aes.IV, 0, combined, 0, aes.IV.Length);
        Buffer.BlockCopy(cipher, 0, combined, aes.IV.Length, cipher.Length);

        return Convert.ToBase64String(combined);
    }
}
