namespace SharedKernel.CardTaxonomy;

/// <summary>
/// Kanonik kart tipi — çözümde TEK tanım. Payment seti (Debit=0, Credit=1) korunur; Commission'ın
/// PREPAID'ini kaybetmemek için <c>Prepaid=2</c> superset olarak eklendi (research R5, kullanıcı onayı).
/// Payment verisinde Prepaid yok → genişletme Payment'ı etkilemez; Commission grid'i remap eder.
/// </summary>
public enum CardType
{
    Unknown = -1,
    Debit = 0,
    Credit = 1,
    Prepaid = 2
}