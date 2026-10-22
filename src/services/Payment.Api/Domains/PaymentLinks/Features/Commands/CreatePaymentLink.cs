using System.Security.Cryptography;
using Payment.Api.Domains.PaymentLinks;

namespace Payment.Api.Domains.PaymentLinks.Features.Commands;

/// <summary>
/// 089 US1: Store için tek-kullanımlık, TTL'li ödeme linki üretir. Kart bilgisi store/PG'ye girmez; müşteri
/// hosted güven bağlamı sayfasında (sonra ödeme) açar. Yanıt: LinkUrl + PgPaymentRef (opak, sır içermez — İlke VI).
/// Aktif-tekil (FR-003): aynı (MerchantId, OrderRef) için AKTİF link varsa yeni üretilmez, var olan döner
/// (idempotent); terminal (Expired/Cancelled) sonrası yeni link serbest. Yalnız TRY (FR-004), tutar &gt; 0.
/// </summary>
public static class CreatePaymentLink
{
    public record CreatePaymentLinkCommand(
        Guid MerchantId, decimal Amount, string Currency, string OrderRef,
        string ReturnUrl, string StoreName, string OrderSummary);

    public class CreatePaymentLinkResponse
    {
        public string LinkUrl { get; set; } = string.Empty;
        public string PgPaymentRef { get; set; } = string.Empty;
    }

    [Transactional]
    public class CreatePaymentLinkCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreatePaymentLinkResponse>> Handle(
            CreatePaymentLinkCommand cmd, IDocumentSession session, PaymentLinkOptions options, CancellationToken ct)
        {
            var now = DateTime.UtcNow;

            // Aktif-tekil (FR-003, research D1): aynı (MerchantId, OrderRef) için AKTİF link'i yükle.
            var existing = await session.Query<PaymentLink>()
                .Where(x => x.MerchantId == cmd.MerchantId && x.OrderRef == cmd.OrderRef &&
                            (x.Status == PaymentLinkStatus.Pending || x.Status == PaymentLinkStatus.Opened))
                .FirstOrDefaultAsync(ct);

            if (existing is not null)
            {
                // Hâlâ geçerli → idempotent: var olanı döndür (SC-002).
                if (now <= existing.ExpiresAt)
                    return Ok(existing, options);

                // Zaman olarak geçmiş ama lazy-flip edilmemiş → Expired'a çevir (partial-unique slot boşalır), devam.
                existing.Expire(now);
                session.Store(existing);
            }

            var openToken = GenerateOpenToken();
            var expiresAt = now + options.Ttl;

            var created = PaymentLink.Create(
                cmd.MerchantId, cmd.OrderRef, cmd.Amount, cmd.Currency, cmd.ReturnUrl,
                cmd.StoreName, cmd.OrderSummary, openToken, expiresAt);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<CreatePaymentLinkResponse>.Error(created.Messages);

            // true-race backstop: eşzamanlı ikinci create partial-unique (MerchantId,OrderRef aktif) kısıtına
            // çakışır → commit başarısız (bütünlük korunur, çift link üretilmez). Ardışık retry zaten yukarıdaki
            // pre-check ile idempotent döner (SC-002).
            session.Store(created.Data!);
            return Ok(created.Data!, options);
        }

        private static FeatureObjectResultModel<CreatePaymentLinkResponse> Ok(PaymentLink link, PaymentLinkOptions options)
            => FeatureObjectResultModel<CreatePaymentLinkResponse>.Ok(new CreatePaymentLinkResponse
            {
                LinkUrl = $"{options.LinkBaseUrl}/{link.OpenToken}",
                PgPaymentRef = link.Id.ToString()
            });

        // Tahmin-edilemez 256-bit URL-safe açılış sırrı (İlke V anonim fail-closed yüzey; İlke VI — log'a düşmez).
        private static string GenerateOpenToken()
            => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
