namespace Payment.Api.Domains.StoredCards;

/// <summary>
/// Kart markası (031) — PAN prefix'inden türetilir (<see cref="CardVault.BrandDetector"/>).
/// BC-içi enum: eski paylaşılan kart taksonomisi (SharedKernel) 021'de silindi; yalnız gösterim/
/// denetim alanı olduğundan cross-BC taksonomi gerekmez.
/// </summary>
public enum CardBrand
{
    Unknown = 0,
    Visa = 1,
    MasterCard = 2,
    Amex = 3,
    Troy = 4
}
