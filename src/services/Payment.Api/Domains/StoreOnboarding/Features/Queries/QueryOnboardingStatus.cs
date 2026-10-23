using Payment.Api.Domains.StoreOnboarding;

namespace Payment.Api.Domains.StoreOnboarding.Features.Queries;

/// <summary>
/// 091 US1 (FR-013): Store aktivasyon durumunu sorgular (store-api-key). Opak PgStoreRef + genel durum +
/// per-banka durum/red sebebi döner. Credential/sır/iç token İÇERMEZ (İlke VI — kontrol düzlemi sır döndürmez).
/// Red sebepleri görünür (US3). Salt okuma.
/// </summary>
public static class QueryOnboardingStatus
{
    public record Query(string PgStoreRef);

    public record BankStatusItem(string BankCode, string Status, string? ReasonCode);

    public class Response
    {
        public string PgStoreRef { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<BankStatusItem> Banks { get; set; } = new();
    }

    public class QueryOnboardingStatusHandler
    {
        public async Task<FeatureObjectResultModel<Response>> Handle(
            Query query, IDocumentSession session, CancellationToken ct)
        {
            if (!Guid.TryParse(query.PgStoreRef, out var id))
                return FeatureObjectResultModel<Response>.Error(new MessageItem
                { Code = StoreOnboardingResourceConstants.ONBOARDING_NOT_FOUND });

            var onboarding = await session.LoadAsync<StoreOnboarding>(id, ct);
            if (onboarding is null)
                return FeatureObjectResultModel<Response>.Error(new MessageItem
                { Code = StoreOnboardingResourceConstants.ONBOARDING_NOT_FOUND });

            return FeatureObjectResultModel<Response>.Ok(new Response
            {
                PgStoreRef = onboarding.Id.ToString(),
                Status = onboarding.Status.ToString(),
                Banks = onboarding.Applications
                    .Select(a => new BankStatusItem(a.BankCode, a.Status.ToString(), a.ReasonCode)).ToList()
            });
        }
    }
}
