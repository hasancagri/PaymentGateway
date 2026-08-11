namespace Commission.Api.Domains.CommissionDrafts.ValueObjects;

/// <summary>
/// Tek revizyon işlemi (contracts §1 <c>revise_commission_draft</c> şeması). LLM yalnız admin'in
/// AÇIK değerlerini taşır; hesap (delta, taban) sunucuda. Adresleme üç biçimden biri:
/// satır no (<see cref="Row"/>), banka+taksit (<see cref="Bank"/>+<see cref="Installment"/>) veya
/// filtre (<see cref="Filter"/> — bank ve/veya installment).
/// </summary>
public record DraftOperation
{
    /// <summary>"set" (oran ata) veya "delta" (orana puan ekle/çıkar).</summary>
    public string Op { get; init; } = string.Empty;

    /// <summary>Satır-no adresleme (1-tabanlı).</summary>
    public int? Row { get; init; }

    /// <summary>Banka adresleme (ad veya kod; büyük/küçük harf duyarsız). Installment ile birlikte kullanılır.</summary>
    public string? Bank { get; init; }

    /// <summary>Taksit adresleme (Bank ile birlikte).</summary>
    public int? Installment { get; init; }

    /// <summary>Toplu adresleme filtresi (bank ve/veya installment).</summary>
    public DraftOperationFilter? Filter { get; init; }

    /// <summary>set için hedef oran.</summary>
    public decimal? Rate { get; init; }

    /// <summary>delta için puan farkı (negatif = düşür).</summary>
    public decimal? Delta { get; init; }
}

/// <summary>Toplu işlem filtresi: iki alan da opsiyonel; dolu olanlar birlikte (AND) eşleşir.</summary>
public record DraftOperationFilter
{
    public string? Bank { get; init; }
    public int? Installment { get; init; }
}