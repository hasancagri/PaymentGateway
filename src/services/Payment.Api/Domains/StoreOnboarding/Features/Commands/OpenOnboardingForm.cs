using Payment.Api.Domains.StoreOnboarding;

namespace Payment.Api.Domains.StoreOnboarding.Features.Commands;

/// <summary>
/// 091 US1 (FR-002/FR-003): Store gizli OpenToken ile hosted formu açar. Bilinmeyen/sahte token hiçbir süreci
/// açamaz (fail-closed, İlke V). Yalnız Draft + süresi geçmemiş form açılır; Submitted/Live ya da expired →
/// açılmaz. Başarıda form render'ı için StoreName döner (uç sayfayı kurar). Durum DEĞİŞTİRMEZ (salt okuma).
/// </summary>
public static class OpenOnboardingForm
{
    public record OpenOnboardingFormCommand(string OpenToken);

    public class OpenOnboardingFormResponse
    {
        public string StoreName { get; set; } = string.Empty;
    }

    [Transactional]
    public class OpenOnboardingFormCommandHandler
    {
        public async Task<FeatureObjectResultModel<OpenOnboardingFormResponse>> Handle(
            OpenOnboardingFormCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var onboarding = await session.Query<StoreOnboarding>()
                .Where(x => x.OpenToken == cmd.OpenToken)
                .FirstOrDefaultAsync(ct);

            // Bilinmeyen token / zaten gönderilmiş / canlı → form açılmaz (fail-closed, FR-007 aynası).
            if (onboarding is null || onboarding.Status != OnboardingStatus.Draft)
                return FeatureObjectResultModel<OpenOnboardingFormResponse>.Error(new MessageItem
                { Code = StoreOnboardingResourceConstants.ONBOARDING_NOT_FOUND });

            if (DateTime.UtcNow > onboarding.ExpiresAt)
                return FeatureObjectResultModel<OpenOnboardingFormResponse>.Error(new MessageItem
                { Code = StoreOnboardingResourceConstants.LINK_EXPIRED });

            return FeatureObjectResultModel<OpenOnboardingFormResponse>.Ok(new OpenOnboardingFormResponse
            {
                StoreName = onboarding.StoreName
            });
        }
    }
}
