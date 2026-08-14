namespace Payment.Api.CardVault;

/// <summary>
/// PAN enc-at-rest soyutlaması. Tokenize anında ham PAN'ı korunmuş forma çevirir
/// (<see cref="Domains.StoredCards.StoredCard.EncryptedPan"/>). Gerçek KMS/HSM sonradan bu seam
/// ardında değişir — aggregate/handler dokunmaz. Bu feature'da yalnız <see cref="Protect"/>
/// kullanılır (Reveal gerçek charge feature'ında gelir; PAN write-only tutulur = min sızma yüzeyi).
/// </summary>
public interface IPanProtector
{
    string Protect(string pan);
}
