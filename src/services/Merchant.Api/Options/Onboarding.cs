using System.ComponentModel.DataAnnotations;

namespace Merchant.Api.Options;

// Onboarding akış ayarları (admin bildirim + aktivasyon linki). Runtime doğrudan IConfiguration
// okuması yasak (CLAUDE.md) — bu POCO Program.cs'te BindConfiguration(nameof(Onboarding)) ile bağlanır.
public class Onboarding
{
    /// <summary>Aktivasyon redeem linkinin taban adresi (ApproveRegisterRequest).</summary>
    [Required]
    public required string ActivationBaseUrl { get; set; }
}
