namespace Payment.Api.Domains.BinCards;

/// <summary>
/// Bir BIN numarasının kart/banka nitelikleri. Referans/lookup verisi — davranış-zengin
/// aggregate DEĞİL. Marten document; kimlik = <see cref="BinNumber"/> (Program.cs'te ayarlı).
/// </summary>
public class BinCard
{
    /// <summary>6 haneli BIN. Marten kimliği (exact-match erişim).</summary>
    public string BinNumber { get; set; } = string.Empty;

    /// <summary>4 haneli banka kodu (PosAccount.BankCode ile aynı kod uzayı; yerel referans).</summary>
    public string BankCode { get; set; } = string.Empty;

    public CardType CardType { get; set; }

    public CardBrand CardBrand { get; set; }

    /// <summary>Taksit-banka türetme sorgusunun anahtarı (Program.cs'te indexli).</summary>
    public CardProgram CardProgram { get; set; }

    /// <summary>Ticari kart mı.</summary>
    public bool Commercial { get; set; }
}