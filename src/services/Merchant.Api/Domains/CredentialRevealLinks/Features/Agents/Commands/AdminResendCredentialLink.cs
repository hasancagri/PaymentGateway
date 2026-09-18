namespace Merchant.Api.Domains.CredentialRevealLinks.Features.Agents.Commands;

// 045 US2/FR-007: yeniden teslim — Approved/var olan merchant için yeni tek gösterimlik link
// üretir, yaşayan eski linkleri öldürür, merchant e-postasına mail atar. Yanıtta key/link
// içeriği YER ALMAZ (yalnız "mail gönderildi").
public static class AdminResendCredentialLink
{
    [RequiredScope(AuthorizationScopes.MerchantAdmin)]
    public record AdminResendCredentialLinkCommand(Guid MerchantId);

    public class AdminResendCredentialLinkResponse
    {
        public bool Sent { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }

    [Transactional]
    public class AdminResendCredentialLinkCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminResendCredentialLinkResponse>> Handle(
            AdminResendCredentialLinkCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            Merchant.Api.Options.Onboarding onboarding,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Domains.Merchants.Merchant>(cmd.MerchantId, ct);
            if (merchant is null)
                return FeatureObjectResultModel<AdminResendCredentialLinkResponse>.NotFound();

            var now = DateTimeOffset.UtcNow;

            // Yeni link eskiyi öldürür (aynı anda tek yaşayan teslim linki).
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
                return FeatureObjectResultModel<AdminResendCredentialLinkResponse>.Error(reveal.Messages);
            session.Store(reveal.Data!);

            await bus.PublishAsync(new Shared.IntegrationEvents.SendEmailRequested(
                merchant.Email,
                "PG erişim bilgileriniz — yeni teslim bağlantısı",
                $"""
                 <p>Merhaba {merchant.ContactName},</p>
                 <p>Erişim bilgileriniz (MerchantId + MerchantKey) için yeni bir teslim bağlantısı
                 oluşturuldu; önceki bağlantılar geçersizdir. Bilgiler aşağıdaki bağlantıda
                 <strong>bir kez</strong> gösterilir:</p>
                 <p><a href="{onboarding.PublicBaseUrl.TrimEnd('/')}/onboarding/reveal/{reveal.Data!.Token}">
                 Erişim bilgilerini görüntüle</a></p>
                 <p>Bağlantı {(int)onboarding.RevealLinkLifetime.TotalMinutes} dakika geçerlidir ve tek
                 kullanımlıktır. Bilgileri gördükten sonra mağazanızın merchant-bilgisi ekranına girin.</p>
                 """,
                IsHtml: true));

            return FeatureObjectResultModel<AdminResendCredentialLinkResponse>.Ok(
                new AdminResendCredentialLinkResponse { Sent = true, ExpiresAt = reveal.Data!.ExpiresAt });
        }
    }
}

/// <summary>045 — merchant'a yeni tek gösterimlik credential teslim bağlantısı mail'ler; eskileri öldürür.</summary>
[McpServerToolType]
public static class AdminResendCredentialLinkMcpTool
{
    [McpServerTool(Name = Shared.MerchantAdminTools.ResendCredentialLink)]
    [Description("Merchant'ın erişim bilgileri (MerchantId + MerchantKey) için YENİ tek kullanımlık " +
                 "teslim bağlantısı üretir ve merchant e-postasına mail'ler; yaşayan eski bağlantılar " +
                 "geçersiz olur. Key/link yanıtında DÖNMEZ — teslim yalnız mail + tek gösterimlik " +
                 "sayfa yoluyladır. Kullanım: teslim maili kaybolduğunda ya da süresi dolduğunda.")]
    public static Task<FeatureObjectResultModel<AdminResendCredentialLink.AdminResendCredentialLinkResponse>>
        AdminResendCredentialLinkAsync(
            [Description("Merchant kimliği (admin_get_merchants ile bulunabilir)")] Guid merchantId,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminResendCredentialLink.AdminResendCredentialLinkResponse>>(
            new AdminResendCredentialLink.AdminResendCredentialLinkCommand(merchantId), ct);
}
