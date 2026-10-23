using System.Security.Cryptography;
using System.Text;
using Common.Dependencies;
using Payment.Api.Domains.StoreOnboarding.ValueObjects;

namespace Payment.Api.Domains.StoreOnboarding.Credentials;

/// <summary>
/// 091 (İlke VI, anayasa v3.1.0 runtime-teslim sır): credential'ı AES-256-GCM ile at-rest şifreleyip Marten
/// dokümanı olarak saklar. DEK (CredentialEncryptionOptions.Key) secret-store'dan; ciphertext DEK olmadan
/// kullanılamaz → ciphertext sır değildir. Plaintext yalnız GetAsync içinde (çekim anında) bellekte çözülür.
/// Save session.Store ile yazar (SaveChanges YOK — çağıran [Transactional] handler'ın outbox commit'inde gider).
/// Sır ASLA log/trace/DTO'da. IScopedDependency → Scrutor IBankCredentialStore olarak kaydeder.
/// </summary>
public sealed class EncryptedBankCredentialStore : IBankCredentialStore, IScopedDependency
{
    private readonly IDocumentSession _session;
    private readonly byte[] _key;

    public EncryptedBankCredentialStore(IDocumentSession session, CredentialEncryptionOptions options)
    {
        _session = session;
        _key = Convert.FromBase64String(options.Key);
    }

    public Task<Guid> SaveAsync(Guid merchantId, string bankCode, BankMerchantCredential credential, CancellationToken ct)
    {
        // VO'nun private-ctor'u manuel deserialize'ı zorlaştırır → düz DTO ile şifrele, Get'te Create ile geri kur.
        var dto = new CredentialDto(credential.ShopCode, credential.UserCode, credential.UserPass,
            credential.SecretKey, credential.SubscriptionKey);
        var plaintext = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(dto));
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var ciphertext = new byte[plaintext.Length];

        using (var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize))
            aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var doc = new StoredBankCredential
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            BankCode = bankCode,
            Ciphertext = Convert.ToBase64String(ciphertext),
            Nonce = Convert.ToBase64String(nonce),
            Tag = Convert.ToBase64String(tag)
        };
        _session.Store(doc); // commit = çağıran [Transactional] handler'ın outbox transaction'ı
        return Task.FromResult(doc.Id);
    }

    public async Task<BankMerchantCredential?> GetAsync(Guid credentialRef, CancellationToken ct)
    {
        var doc = await _session.LoadAsync<StoredBankCredential>(credentialRef, ct);
        if (doc is null) return null;

        var ciphertext = Convert.FromBase64String(doc.Ciphertext);
        var nonce = Convert.FromBase64String(doc.Nonce);
        var tag = Convert.FromBase64String(doc.Tag);
        var plaintext = new byte[ciphertext.Length];

        using (var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize))
            aes.Decrypt(nonce, ciphertext, tag, plaintext);

        var dto = JsonConvert.DeserializeObject<CredentialDto>(Encoding.UTF8.GetString(plaintext));
        return dto is null ? null
            : BankMerchantCredential.Create(dto.ShopCode, dto.UserCode, dto.UserPass, dto.SecretKey, dto.SubscriptionKey);
    }

    // Şifrelenecek düz taşıma (VO private-ctor'suz serileşsin diye). Yalnız ciphertext içinde yaşar.
    private sealed record CredentialDto(string ShopCode, string UserCode, string UserPass, string? SecretKey, string? SubscriptionKey);
}

// At-rest şifreli credential dokümanı. Ciphertext/Nonce/Tag = AES-GCM; plaintext credential ASLA burada değil.
// Kimlik = Id (CredentialRef); BankApplication.CredentialRef buna işaret eder. MerchantId/BankCode lookup içindir.
public class StoredBankCredential
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public string BankCode { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
}
