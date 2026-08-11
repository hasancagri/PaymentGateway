using System.ComponentModel.DataAnnotations;

namespace Commission.Api.Options;

// 019: komisyon teklifi ayarları. Runtime doğrudan IConfiguration okuması yasak (CLAUDE.md) —
// bu POCO OptionsExt.AddOptionsExt ile BindConfiguration(nameof(CommissionProposalOption)) bağlanır.
public class CommissionProposalOption
{
    /// <summary>Banka oranının üstüne eklenen sabit marj (puan; ör. 0.5 → banka 1.75 ⇒ teklif 2.25).</summary>
    [Required]
    [Range(0, 100)]
    public required decimal DefaultMarginPoints { get; set; }

    /// <summary>Karar biletinin geçerlilik süresi (saat).</summary>
    [Required]
    [Range(1, 24 * 365)]
    public required int TicketTtlHours { get; set; }

    /// <summary>Mail'e yazılan mutlak kabul/ret linklerinin taban adresi (Commission.Api dış adresi).</summary>
    [Required]
    public required string PublicBaseUrl { get; set; }
}