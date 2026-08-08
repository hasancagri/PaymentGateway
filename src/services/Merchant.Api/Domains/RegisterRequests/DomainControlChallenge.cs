namespace Merchant.Api.Domains.RegisterRequests;

/// <summary>
/// Tek-kullanımlık, süreli domain-control kanıtı bileti (HTTP-01 tarzı — 013 D3). RegisterRequest'ten
/// ÖNCE yaşar: doğrulama geçmeden talep OLUŞMAZ. Gateway <see cref="Token"/> + <see cref="ExpectedValue"/>
/// üretir; aday <c>/.well-known/merchant-challenge/{token}</c> yolunda ExpectedValue'yu yayınlar;
/// gateway senkron GET ile doğrular. TTL ~1 saat. Consumed/Expired tekrar doğrulanamaz.
/// </summary>
public class DomainControlChallenge : AggregateRoot
{
    private DomainControlChallenge()
    {
    }

    /// <summary>Bilet TTL'i (saat).</summary>
    public const int TtlHours = 1;

    public string Domain { get; private set; } = string.Empty;
    public string Token { get; private set; } = string.Empty;
    public string ExpectedValue { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public ChallengeStatus Status { get; private set; } = ChallengeStatus.Issued;

    /// <summary>
    /// Yeni bilet üretir. Token = challenge dosya adı (URL-güvenli tek-kullanım); ExpectedValue = adayın
    /// o yolda yayınlayacağı rastgele değer. Aday değeri yayınlayabiliyorsa siteyi kontrol ediyordur.
    /// </summary>
    public static DomainControlChallenge Issue(string domain)
    {
        return new DomainControlChallenge
        {
            Domain = Normalize(domain),
            Token = Guid.NewGuid().ToString("N"),
            ExpectedValue = Guid.NewGuid().ToString("N"),
            ExpiresAtUtc = DateTime.UtcNow.AddHours(TtlHours),
            Status = ChallengeStatus.Issued
        };
    }

    /// <summary>
    /// Adayın yayınladığı değeri doğrular. Süre + tam byte eşleşmesi. Başarı → Consumed (tek-kullanım),
    /// <see cref="ChallengeOutcome.Passed"/>. Süre dolmuş → Expired. Değer yok/yanlış → Failed (bilet
    /// Issued kalır, aday yayınlayıp tekrar deneyebilir). Consumed/Expired tekrar doğrulanamaz.
    /// </summary>
    public ChallengeOutcome Verify(string? fetchedValue, DateTime nowUtc)
    {
        if (Status == ChallengeStatus.Consumed)
            return ChallengeOutcome.Passed;
        if (Status == ChallengeStatus.Expired)
            return ChallengeOutcome.Expired;

        if (nowUtc > ExpiresAtUtc)
        {
            Status = ChallengeStatus.Expired;
            UpdatedTime = DateTime.UtcNow;
            return ChallengeOutcome.Expired;
        }

        if (!string.IsNullOrWhiteSpace(fetchedValue) &&
            string.Equals(fetchedValue.Trim(), ExpectedValue, StringComparison.Ordinal))
        {
            Status = ChallengeStatus.Consumed;
            UpdatedTime = DateTime.UtcNow;
            return ChallengeOutcome.Passed;
        }

        return ChallengeOutcome.Failed;
    }

    private static string Normalize(string domain) => domain.Trim().ToLowerInvariant();
}

/// <summary>Bilet iç durumu (RegisterRequest'in ChallengeOutcome'undan ayrı).</summary>
public enum ChallengeStatus
{
    Issued = 1,
    Consumed = 2,
    Expired = 3
}

/// <summary>Doğrulama sonucu (RegisterRequest yalnız Passed ile oluşur).</summary>
public enum ChallengeOutcome
{
    Pending = 1,
    Passed = 2,
    Failed = 3,
    Expired = 4
}