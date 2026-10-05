using System.Security.Claims;

namespace Merchant.Api.Domains.Merchants.Features.Commands;

// 046/087 US1: merchant self-servis key yenileme (store S2S tetik, ecommerce-onboarding m2m). Merchant
// kimliğiyle taze key üretir → MerchantKeyReissued outbox'la yayınlanır (Identity client_secret + Payment
// KeyHash güncellenir → eski key HER temsilde anında ölür) → 087: yeni key reveal sayfası yerine store
// callbackUrl'ine HMAC-imzalı dayanıklı callback'le teslim edilir (echo correlationId). Reveal-link SÖKÜLDÜ;
// S2S dönüşü yalnız kabul-makbuzu (key/RevealUrl İÇERMEZ, FR-A1/A2).
public static class ReissueMerchantKey
{
    // v1 = çıplak merchantId (kontrat); US3 ad/e-posta çözümlemeli varyant. 087: correlationId +
    // callbackUrl register ile simetrik (store sağlar, PG echo'lar).
    public record ReissueMerchantKeyCommand(
        Guid MerchantId, Guid CorrelationId, string CallbackUrl, string? Reason, string InitiatedBy);

    public class ReissueMerchantKeyResponse
    {
        public bool Accepted { get; set; }
        public Guid CorrelationId { get; set; }
    }

    [Transactional]
    public class ReissueMerchantKeyCommandHandler
    {
        public async Task<FeatureObjectResultModel<ReissueMerchantKeyResponse>> Handle(
            ReissueMerchantKeyCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
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

            // 087 US1: yeni key store callbackUrl'ine HMAC-imzalı dayanıklı teslim (reveal SÖKÜLDÜ;
            // echo correlationId). Aynı transaction: commit'siz callback gitmez (outbox).
            await bus.PublishAsync(new DeliverCredentialCallback.Deliver(
                cmd.CallbackUrl, cmd.CorrelationId, merchant.Id, merchant.MerchantKey, "Active"));

            // 046 US2/FR-007: salt-append denetim kaydı (aynı [Transactional] commit).
            session.Store(new Domains.MerchantKeyReissueLogs.MerchantKeyReissueLog
            {
                Id = Guid.NewGuid(),
                MerchantId = merchant.Id,
                ReissuedAt = DateTimeOffset.UtcNow,
                InitiatedBy = cmd.InitiatedBy,
                Reason = string.IsNullOrWhiteSpace(cmd.Reason) ? null : cmd.Reason.Trim()
            });

            // Dönüş credential/RevealUrl-free — yalnız kabul makbuzu (teslim callback'le asenkron).
            return FeatureObjectResultModel<ReissueMerchantKeyResponse>.Ok(new ReissueMerchantKeyResponse
            {
                Accepted = true,
                CorrelationId = cmd.CorrelationId
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
                        new ReissueMerchantKeyCommand(
                            merchantId.Value, body.CorrelationId, body.CallbackUrl, body.Reason, initiatedBy));
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
    // 087: correlationId + callbackUrl register ile simetrik (store sağlar; callback teslim hedefi).
    public record ReissueRequest(
        Guid? MerchantId, string? MerchantName, string? Email, string? Reason,
        Guid CorrelationId, string CallbackUrl);
}