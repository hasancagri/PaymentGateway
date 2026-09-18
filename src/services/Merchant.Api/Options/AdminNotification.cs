using System.ComponentModel.DataAnnotations;

namespace Merchant.Api.Options;

// 043 FR-011: submit_registration sonrası bilgilendirme mailinin sabit alıcısı. Runtime doğrudan
// IConfiguration okuması yasak (CLAUDE.md) — bu POCO Program.cs'te BindConfiguration(nameof(AdminNotification))
// ile bağlanır (Onboarding Options'ıyla aynı desen).
public class AdminNotification
{
    /// <summary>Kayıt başvurusu bildirim mailinin gideceği sabit admin adresi.</summary>
    [Required]
    public required string AdminEmail { get; set; }
}
