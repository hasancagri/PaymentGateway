namespace Payment.Api.Domains.StoredCards;

/// <summary>
/// Kayıtlı kart yaşam-döngüsü. Silme soft: <see cref="Revoked"/> fiziksel kaydı durdurur
/// (resolve/update RET). Reactivate yok — yeni kart = yeni tokenize.
/// </summary>
public enum StoredCardStatus
{
    Active = 0,
    Revoked = 1,
}
