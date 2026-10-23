using System.Security.Cryptography;
using Payment.Api.Domains.StoreOnboarding.BankOnboarding;
using Payment.Api.Domains.StoreOnboarding.ValueObjects;
using Payment.Api.Options;

namespace Payment.Api.Domains.StoreOnboarding;

/// <summary>
/// 091 DEV-ONLY: onboarding handshake'ini (EC↔PG↔banka onay/red trafiği) atlayıp "baştan hazır" bir
/// <b>Live</b> store seed'ler — test hızlandırma. YALNIZ Development'ta çağrılır (Program.cs IsDevelopment
/// kapısı). Live durumu aggregate'in KENDİ metotlarıyla kurulur (Create→SubmitProfile→RecordBankOutcome
/// Approved) → invariant'lar korunur, kestirme setter yok. Banka credential'ı seed'lenmez (sır yok; charge
/// D9 wiring ayrı). Idempotent: aktif süreç varsa dokunmaz. Prod'da ASLA çalışmaz.
/// </summary>
public static class StoreOnboardingDevSeeder
{
    private const string SeedIban = "TR330006100519786457841326"; // mod-97 geçerli

    public static async Task SeedAsync(IDocumentSession session, StoreOnboardingDevSeedOptions options, CancellationToken ct = default)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.MerchantId) ||
            string.IsNullOrWhiteSpace(options.BankCode) || !Guid.TryParse(options.MerchantId, out var merchantId))
            return;

        // Idempotent: bu merchant için aktif (Draft/Submitted/Live) süreç varsa reseed etme.
        var existing = await session.Query<StoreOnboarding>()
            .Where(x => x.MerchantId == merchantId &&
                        (x.Status == OnboardingStatus.Draft || x.Status == OnboardingStatus.Submitted ||
                         x.Status == OnboardingStatus.Live))
            .AnyAsync(ct);
        if (existing) return;

        var now = DateTime.UtcNow;
        var created = StoreOnboarding.Create(
            merchantId, $"dev-seed-{merchantId}", options.StoreName, Token(), Token(), now.AddHours(24));
        if (!created.IsSuccess) return;
        var onboarding = created.Data!;

        var profile = StoreProfile.Create(
            options.StoreName, "1234567890", SeedIban, "5411", "https://dev.example", "Dev Admin", "dev@dev.example");
        if (!profile.IsSuccess) return;

        onboarding.SubmitProfile(profile.Data!,
            new List<(string, OnboardingOutcome)> { (options.BankCode, OnboardingOutcome.Submitted("dev-seed-ref")) }, now);
        onboarding.RecordBankOutcome(options.BankCode,
            OnboardingOutcome.Approved(BankMerchantCredential.Create("DEV", "DEV", "DEV"), "dev-seed-ref"), null, now);

        session.Store(onboarding);
        await session.SaveChangesAsync(ct);
    }

    private static string Token()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
