namespace Payment.Api.Domains.StoredCards;

/// <summary>
/// iyzico <c>CardAssociation</c> string'ini BC-içi <see cref="CardBrand"/>'e eşler (anti-corruption
/// mapping; 032 Model A — marka iyzico yanıtından gelir). Saf, deterministik.
/// </summary>
public static class CardAssociationMapper
{
    public static CardBrand Map(string? association) => (association ?? string.Empty).ToUpperInvariant() switch
    {
        "VISA" => CardBrand.Visa,
        "MASTER_CARD" => CardBrand.MasterCard,
        "AMERICAN_EXPRESS" => CardBrand.Amex,
        "TROY" => CardBrand.Troy,
        _ => CardBrand.Unknown
    };
}
