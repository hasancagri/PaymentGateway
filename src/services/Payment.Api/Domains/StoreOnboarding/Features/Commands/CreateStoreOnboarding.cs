using System.Security.Cryptography;
using Payment.Api.Domains.StoreOnboarding;

namespace Payment.Api.Domains.StoreOnboarding.Features.Commands;

/// <summary>
/// 091 US1 (FR-001/FR-002): EC (super-admin adına, store-api-key) store aktivasyonunu başlatır. Draft bir
/// StoreOnboarding + tahmin-edilemez OpenToken (form açılışı) + CallbackToken (banka→PG webhook kapısı) üretir;
/// yanıt opak PgStoreRef + form linki (credential/sır İÇERMEZ — İlke VI). Aktif-tekil (FR-014): aynı MerchantId
/// için aktif (Draft/Submitted/Live) süreç varsa yeni üretilmez, var olan döner (idempotent); partial-unique backstop.
/// </summary>
public static class CreateStoreOnboarding
{
    public record CreateStoreOnboardingCommand(Guid MerchantId, string CorrelationId, string StoreName);

    public class CreateStoreOnboardingResponse
    {
        public string PgStoreRef { get; set; } = string.Empty;
        public string FormUrl { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    [Transactional]
    public class CreateStoreOnboardingCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateStoreOnboardingResponse>> Handle(
            CreateStoreOnboardingCommand cmd, IDocumentSession session, StoreOnboardingOptions options, CancellationToken ct)
        {
            // Aktif-tekil (FR-014): aynı store için aktif süreç varsa idempotent döndür (çift kayıt yok).
            var existing = await session.Query<StoreOnboarding>()
                .Where(x => x.MerchantId == cmd.MerchantId &&
                            (x.Status == OnboardingStatus.Draft || x.Status == OnboardingStatus.Submitted ||
                             x.Status == OnboardingStatus.Live))
                .FirstOrDefaultAsync(ct);

            if (existing is not null)
                return Ok(existing, options);

            var now = DateTime.UtcNow;
            var created = StoreOnboarding.Create(
                cmd.MerchantId, cmd.CorrelationId, cmd.StoreName,
                GenerateToken(), GenerateToken(), now + options.Ttl);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<CreateStoreOnboardingResponse>.Error(created.Messages);

            // true-race backstop: eşzamanlı ikinci create partial-unique (MerchantId aktif) kısıtına çakışır →
            // commit başarısız (çift süreç üretilmez); ardışık retry yukarıdaki pre-check ile idempotent döner.
            session.Store(created.Data!);
            return Ok(created.Data!, options);
        }

        private static FeatureObjectResultModel<CreateStoreOnboardingResponse> Ok(StoreOnboarding o, StoreOnboardingOptions options)
            => FeatureObjectResultModel<CreateStoreOnboardingResponse>.Ok(new CreateStoreOnboardingResponse
            {
                PgStoreRef = o.Id.ToString(),
                FormUrl = $"{options.FormBaseUrl}/{o.OpenToken}",
                ExpiresAt = o.ExpiresAt
            });

        // Tahmin-edilemez 256-bit URL-safe sır (İlke V anonim fail-closed yüzey; İlke VI — log'a düşmez).
        private static string GenerateToken()
            => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
