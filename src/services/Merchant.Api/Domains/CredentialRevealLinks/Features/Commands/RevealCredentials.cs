namespace Merchant.Api.Domains.CredentialRevealLinks.Features.Commands;

// 045 US2: teslim sayfasının GET'i — GÖSTERİM TÜKETİR (gösterim = teslim; ikinci açılış ölü).
// MerchantKey yalnız bu yanıtla sayfaya akar; loglanmaz, başka hiçbir yüzeyde dönmez.
public static class RevealCredentials
{
    public record RevealCredentialsCommand(string Token);

    public class RevealCredentialsResponse
    {
        public Guid MerchantId { get; set; }
        public string MerchantKey { get; set; } = string.Empty;
    }

    [Transactional]
    public class RevealCredentialsCommandHandler
    {
        public async Task<FeatureObjectResultModel<RevealCredentialsResponse>> Handle(
            RevealCredentialsCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var now = DateTimeOffset.UtcNow;
            var link = await session.Query<CredentialRevealLink>()
                .FirstOrDefaultAsync(x => x.Token == cmd.Token, ct);

            // Bilinmeyen/tüketilmiş/süresi geçmiş token → nötr NotFound (doğruluk sızdırılmaz).
            if (link is null || !link.IsUsable(now))
                return FeatureObjectResultModel<RevealCredentialsResponse>.NotFound();

            var merchant = await session.LoadAsync<Domains.Merchants.Merchant>(link.MerchantId, ct);
            if (merchant is null)
                return FeatureObjectResultModel<RevealCredentialsResponse>.NotFound();

            var consumed = link.Consume(now);
            if (!consumed.IsSuccess)
                return FeatureObjectResultModel<RevealCredentialsResponse>.NotFound();
            session.Store(link);

            return FeatureObjectResultModel<RevealCredentialsResponse>.Ok(new RevealCredentialsResponse
            {
                MerchantId = merchant.Id,
                MerchantKey = merchant.MerchantKey
            });
        }
    }
}
