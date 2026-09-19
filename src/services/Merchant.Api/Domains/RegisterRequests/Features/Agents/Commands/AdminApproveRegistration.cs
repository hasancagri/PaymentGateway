namespace Merchant.Api.Domains.RegisterRequests.Features.Agents.Commands;

// 043 US2: onay — başvuru bilgileriyle Merchant.Create (Active doğar, MerchantKey üretir),
// MerchantCreated outbox'la Identity'ye gider. Mevcut ApproveRegisterRequestCommandHandler ile
// birebir aynı iş kuralı; agent slice kendi kopyasını taşır (bilinçli tekrar — Commands/Queries'e
// IMessageBus ile bile gidilmez, conventions.md).
public static class AdminApproveRegistration
{
    [RequiredScope(AuthorizationScopes.MerchantAdmin)]
    public record AdminApproveRegistrationCommand(Guid RequestId);

    public class AdminApproveRegistrationResponse
    {
        public Guid RequestId { get; set; }
        public string Status { get; set; } = string.Empty;
        public Guid MerchantId { get; set; }
    }

    [Transactional]
    public class AdminApproveRegistrationCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminApproveRegistrationResponse>> Handle(
            AdminApproveRegistrationCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            Merchant.Api.Options.Onboarding onboarding,
            CancellationToken ct)
        {
            var request = await session.LoadAsync<RegisterRequest>(cmd.RequestId, ct);
            if (request is null)
                return FeatureObjectResultModel<AdminApproveRegistrationResponse>.NotFound();

            var merchantResult = Domains.Merchants.Merchant.Create(
                request.Type, request.Name, request.Email, request.GsmNumber, request.Address,
                request.Iban, request.ContactName, request.ContactSurname,
                request.IdentityNumber, request.TaxOffice, request.TaxNumber, request.LegalCompanyTitle);
            if (!merchantResult.IsSuccess)
                return FeatureObjectResultModel<AdminApproveRegistrationResponse>.Error(merchantResult.Messages);

            var merchant = merchantResult.Data!;

            var approveResult = request.Approve(merchant.Id);
            if (!approveResult.IsSuccess)
                return FeatureObjectResultModel<AdminApproveRegistrationResponse>.Error(approveResult.Messages);

            session.Store(merchant);
            session.Store(request);

            // Identity.Server tüketir → OpenIddict istemci kaydı (client_secret = MerchantKey).
            // [Transactional] outbox: yayın yalnız DB commit'te gider.
            await bus.PublishAsync(new Shared.IntegrationEvents.MerchantCreated(
                merchant.Id, merchant.MerchantKey, merchant.Status.ToString()));

            // 045 US2: teslim linki + mail — key sohbet/S2S yanıtına girmez; teslim mail+sayfa
            // yoluyla (tek gösterimlik). Aynı transaction: link commit'siz mail gitmez (outbox).
            var reveal = Domains.CredentialRevealLinks.CredentialRevealLink.Create(
                merchant.Id, onboarding.RevealLinkLifetime);
            if (!reveal.IsSuccess)
                return FeatureObjectResultModel<AdminApproveRegistrationResponse>.Error(reveal.Messages);
            session.Store(reveal.Data!);

            await bus.PublishAsync(new Shared.IntegrationEvents.SendEmailRequested(
                request.Email,
                "PG merchant kaydınız onaylandı — erişim bilgileriniz",
                $"""
                 <p>Merhaba {request.ContactName},</p>
                 <p>Ödeme gateway'i kayıt başvurunuz onaylandı. Erişim bilgileriniz
                 (MerchantId + MerchantKey) aşağıdaki bağlantıda <strong>bir kez</strong> gösterilir:</p>
                 <p><a href="{onboarding.PublicBaseUrl.TrimEnd('/')}/onboarding/reveal/{reveal.Data!.Token}">
                 Erişim bilgilerini görüntüle</a></p>
                 <p>Bağlantı {(int)onboarding.RevealLinkLifetime.TotalMinutes} dakika geçerlidir ve tek
                 kullanımlıktır. Bilgileri gördükten sonra mağazanızın merchant-bilgisi ekranına girin.
                 Bağlantının süresi dolarsa gateway yöneticisinden yeni bağlantı isteyin.</p>
                 """,
                IsHtml: true));

            return FeatureObjectResultModel<AdminApproveRegistrationResponse>.Ok(new AdminApproveRegistrationResponse
            {
                RequestId = request.Id,
                Status = request.Status.ToString(),
                MerchantId = merchant.Id
            });
        }
    }
}

/// <summary>US2 — kayıt başvurusunu onaylar; yalnız Pending'den (terminalde INVALID_OPERATION_ERROR).</summary>
[McpServerToolType]
public static class AdminApproveRegistrationMcpTool
{
    [McpServerTool(Name = Shared.MerchantAdminTools.ApproveRegistration)]
    [Description("Pending başvuruyu onaylar: yeni Active merchant doğar, merchantId döner. " +
                 "Başvuru zaten terminal statüdeyse (Approved/Rejected) INVALID_OPERATION_ERROR döner.")]
    public static Task<FeatureObjectResultModel<AdminApproveRegistration.AdminApproveRegistrationResponse>>
        AdminApproveRegistrationAsync(
            [Description("Onaylanacak başvuru kimliği")] Guid requestId,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminApproveRegistration.AdminApproveRegistrationResponse>>(
            new AdminApproveRegistration.AdminApproveRegistrationCommand(requestId), ct);
}
