namespace SharedKernel.CardTaxonomy;

/// <summary>
/// Kanonik kart markası — çözümde TEK tanım (co-owned sözcük). Değerler Payment (CP.VPOS paritesi)
/// setinden; Payment + Commission bu enum'a referans verir, yerel kopya tutmaz. Commission grid'inin
/// eski int'leri bu kanonik sete migrate edilir.
/// </summary>
public enum CardBrand
{
    Unknown = -1,
    Visa = 0,
    MasterCard = 1,
    Troy = 2,
    Amex = 3,
    Discover = 4,
    Unionpay = 5,
    JCB = 6
}