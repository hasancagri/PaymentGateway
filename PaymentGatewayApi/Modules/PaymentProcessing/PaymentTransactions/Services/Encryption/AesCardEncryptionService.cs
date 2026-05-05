namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Services.Encryption;

public class AesCardEncryptionService : ICardEncryptionService
{
    private readonly byte[] _key;

    public AesCardEncryptionService(IConfiguration configuration)
    {
        var keyBase64 = configuration["CardEncryption:Key"]
            ?? throw new InvalidOperationException("CardEncryption:Key is not configured.");

        _key = Convert.FromBase64String(keyBase64);

        if (_key.Length != 32)
            throw new InvalidOperationException("CardEncryption:Key must be 32 bytes (AES-256).");
    }

    // Format: Base64( IV[16 bytes] + CipherText )
    public string Encrypt(string plainCardNumber)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainCardNumber);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = new byte[aes.IV.Length + cipherBytes.Length];
        aes.IV.CopyTo(result, 0);
        cipherBytes.CopyTo(result, aes.IV.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string encryptedCardNumber)
    {
        var fullBytes = Convert.FromBase64String(encryptedCardNumber);

        var iv = fullBytes[..16];
        var cipherBytes = fullBytes[16..];

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }
}