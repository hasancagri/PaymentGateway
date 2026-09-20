using System.Security.Claims;
using Merchant.Api.Domains.CredentialRevealLinks;

namespace Merchant.Api.Domains.Merchants.Features.Commands;

// 046 US1: merchant self-servis key yenileme (store S2S tetik, ecommerce-onboarding m2m — 045 deseni).
// Merchant kimliğiyle taze key üretir → MerchantKeyReissued outbox'la yayınlanır (Identity client_secret
// + Payment KeyHash güncellenir → eski key HER temsilde anında ölür) → yeni tek gösterimlik teslim linki
// doğar, eski yaşayan linkler öldürülür → yanıt yalnız revealUrl + expiry (key İÇERMEZ, FR-005).
public static class ReissueMerchantKey
{
    // v1 = çıplak merchantId (kontrat). FR-011 ad/e-posta çözümlemeli varyant US3'te eklenir.
    public record ReissueMerchantKeyCommand(Guid MerchantId, string? Reason, string InitiatedBy);

    public class ReissueMerchantKeyResponse
    {
        public string RevealUrl { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
    }

    [Transactional]
    public class ReissueMerchantKeyCommandHandler
    {
        public async Task<FeatureObjectResultModel<ReissueMerchantKeyResponse>> Handle(
            ReissueMerchantKeyCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            global::Merchant.Api.Options.Onboarding onboarding,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(cmd.MerchantId, ct);
            if (merchant is null)
                return FeatureObjectResultModel<ReissueMerchantKeyResponse>.NotFound();

            // FR-009: yalnız Active merchant; değilse key değişmez.
            var reissue = merchant.ReissueKey();
            if (!reissue.IsSuccess)
                return FeatureObjectResultModel<ReissueMerchantKeyResponse>.Error(reissue.Messages);
            session.Store(merchant);

            // Identity.Server + Payment.Api tüketir → client_secret + KeyHash güncellenir (eski key 401).
            // [Transactional] outbox: yayın yalnız DB commit'te gider.
            await bus.PublishAsync(new Shared.IntegrationEvents.MerchantKeyReissued(
                merchant.Id, merchant.MerchantKey));

            var now = DateTimeOffset.UtcNow;

            // FR-010: yeni link eskiyi öldürür (aynı anda tek yaşayan teslim linki).
            var living = await session.Query<CredentialRevealLink>()
                .Where(l => l.MerchantId == merchant.Id && l.ConsumedAt == null && l.ExpiresAt > now)
                .ToListAsync(ct);
            foreach (var old in living)
            {
                old.Kill(now);
                session.Store(old);
            }

            var reveal = CredentialRevealLink.Create(merchant.Id, onboarding.RevealLinkLifetime);
            if (!reveal.IsSuccess)
                return FeatureObjectResultModel<ReissueMerchantKeyResponse>.Error(reveal.Messages);
            session.Store(reveal.Data!);

            // 046 US2/FR-007: salt-append denetim kaydı (aynı [Transactional] commit).
            session.Store(new Domains.MerchantKeyReissueLogs.MerchantKeyReissueLog
            {
                Id = Guid.NewGuid(),
                MerchantId = merchant.Id,
                ReissuedAt = now,
                InitiatedBy = cmd.InitiatedBy,
                Reason = string.IsNullOrWhiteSpace(cmd.Reason) ? null : cmd.Reason.Trim()
            });

            return FeatureObjectResultModel<ReissueMerchantKeyResponse>.Ok(new ReissueMerchantKeyResponse
            {
                RevealUrl = $"{onboarding.PublicBaseUrl.TrimEnd('/')}/onboarding/reveal/{reveal.Data!.Token}",
                ExpiresAt = reveal.Data!.ExpiresAt
            });
        }
    }

    // S2S REST (store → PG): POST api/v1/onboarding/reissue. Auth = ecommerce-onboarding m2m
    // (merchant.write; merchant key ile DEĞİL — chicken-egg). InitiatedBy = token client_id claim'i.
    // US3/FR-011: çıplak merchantId yoksa merchantName/email ile çözümlenir (belirsiz/yok → key değişmez).
    public static RouteGroupBuilder ReissueMerchantKeyGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/reissue",
                async ([FromBody] ReissueRequest body, ClaimsPrincipal principal, IMessageBus bus) =>
                {
                    // client_credentials token: client_id / sub taşır (merchant token bu iç yüzeye
                    // girmez — ecommerce-onboarding m2m). Denetim izinin "kim" alanı.
                    var initiatedBy = principal.FindFirst("client_id")?.Value
                        ?? principal.FindFirst("sub")?.Value
                        ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? "unknown";

                    // US3: çıplak GUID öncelikli; yoksa ad/e-posta tam-eşleşme çözümlemesi (T018).
                    var merchantId = body.MerchantId;
                    if (merchantId is null || merchantId == Guid.Empty)
                    {
                        var resolved = await bus
                            .InvokeAsync<FeatureObjectResultModel<Queries.ResolveMerchantByName.ResolveMerchantByNameResponse>>(
                                new Queries.ResolveMerchantByName.ResolveMerchantByNameQuery(body.MerchantName, body.Email));
                        if (!resolved.IsSuccess)
                            return Results.BadRequest(resolved);
                        merchantId = resolved.Data!.MerchantId;
                    }

                    var result = await bus.InvokeAsync<FeatureObjectResultModel<ReissueMerchantKeyResponse>>(
                        new ReissueMerchantKeyCommand(merchantId.Value, body.Reason, initiatedBy));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("ReissueMerchantKey")
            .MapToApiVersion(1, 0)
            // TODO(FR-012 / S1 rate-limit borcu): abuse'a karşı bu ucun rate-limit policy'si S1
            // (genel rate-limiting) gelince buraya takılır — reissue kötüye kullanım yüzeyidir.
            .RequireAuthorization(AuthorizationScopes.MerchantWrite)
            .Produces<ReissueMerchantKeyResponse>();

        return group;
    }

    // v1 kontrat = merchantId; US3 opsiyonel merchantName/email (biri verilir). reason opsiyonel.
    public record ReissueRequest(Guid? MerchantId, string? MerchantName, string? Email, string? Reason);
}