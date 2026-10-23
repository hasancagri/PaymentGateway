using Payment.Api.Domains.StoreOnboarding.ValueObjects;

namespace Payment.Api.Domains.StoreOnboarding.Credentials;

/// <summary>
/// 091 (İlke VI, anayasa v3.1.0): per-(store,banka) banka credential'ının güvenli saklama soyutlaması.
/// Runtime-teslim sır (banka webhook'uyla gelir) — statik secret-store'a yazılamaz → at-rest şifreli
/// kalıcılaştırılır. Aggregate yalnız opak CredentialRef (Guid) tutar; plaintext credential yalnız çekim
/// yolu (gelecek D9 wiring) Get ile çözer. Save/Get dışında hiçbir yüzey credential'ı materyalize etmez.
/// </summary>
public interface IBankCredentialStore
{
    /// <summary>Credential'ı at-rest şifreli saklar, opak referans (CredentialRef) döner. Sır dönüşte/log'da yok.</summary>
    Task<Guid> SaveAsync(Guid merchantId, string bankCode, BankMerchantCredential credential, CancellationToken ct);

    /// <summary>CredentialRef ile deşifre edilmiş credential'ı döner (yalnız çekim yolu çağırır). Bulunamazsa null.</summary>
    Task<BankMerchantCredential?> GetAsync(Guid credentialRef, CancellationToken ct);
}
