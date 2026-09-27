using Common.Auths;
using Merchant.Api.Options;
using Merchant.Api.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Merchant.Api.Domains.Merchants.Features.Agents.Commands;

// 048 US1/FR-001..FR-003: hassas-veri hosted sayfasına süreli + TEK KULLANIMLIK link üretir. Hassas
// alan (email/iban/tckn/gsm/vergi) YANITA veya sohbete ASLA girmez — yalnız {url, expiresAt, message}.
// Link tabanı config'ten (SensitiveEntryOptions.PublicBaseUrl) — HttpContext base KULLANILMAZ: MCP
// çağrısı store fasadı/gateway proxy'sinden gelir, istek base'i Aspire iç adresidir (D6).
// Denetim izi (FR-010): credential/sensitive_link_created — merchantId + RequestedByUserId; token DEĞİL.
public static class RequestSensitiveLink
{
    [RequiredScope(AuthorizationScopes.MerchantAdmin)]
    public record RequestSensitiveLinkCommand(Guid MerchantId, Guid UserId);

    public class RequestSensitiveLinkResponse
    {
        public string Url { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    [Transactional]
    public class RequestSensitiveLinkCommandHandler
    {
        public async Task<FeatureObjectResultModel<RequestSensitiveLinkResponse>> Handle(
            RequestSensitiveLinkCommand cmd,
            IDocumentSession session,
            SensitiveEntryOptions options,
            ILogger<RequestSensitiveLinkCommandHandler> logger,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(cmd.MerchantId, ct);
            if (merchant is null || merchant.IsDeleted)
                return FeatureObjectResultModel<RequestSensitiveLinkResponse>.NotFound();

            var created = SensitiveEntrySession.Create(cmd.MerchantId, cmd.UserId, options.LinkLifetime);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<RequestSensitiveLinkResponse>.Error(created.Messages);

            var entrySession = created.Data!;
            session.Store(entrySession);

            // Denetim izi: token DEĞİL — hedef merchant + isteyen kimliği (FR-010).
            logger.LogInformation(
                "credential/sensitive_link_created merchantId={MerchantId} requestedByUserId={UserId}",
                cmd.MerchantId, cmd.UserId);

            return FeatureObjectResultModel<RequestSensitiveLinkResponse>.Ok(new RequestSensitiveLinkResponse
            {
                Url = $"{options.PublicBaseUrl.TrimEnd('/')}/merchants/sensitive/{entrySession.Token}",
                ExpiresAt = entrySession.ExpiresAt,
                Message = "Linki tarayıcıda açın; hassas veriler orada görünür/düzenlenir. " +
                          "Link 15 dk geçerli ve tek kullanımlıktır."
            });
        }
    }
}

/// <summary>US1 — hassas-veri sayfası için süreli + tek kullanımlık link üretir; hassas alan yanıta girmez.</summary>
[McpServerToolType]
public static class RequestSensitiveLinkMcpTool
{
    [McpServerTool(Name = MerchantAdminTools.RequestSensitiveLink)]
    [Description(
        "YÖNETİM: bir merchant'ın hassas kişisel/finansal alanlarını (Email/GSM/TCKN/IBAN/vergi no) " +
        "GÖRÜNTÜLEMEK ve DÜZENLEMEK için store'un hosted ekranına SÜRELİ + TEK KULLANIMLIK link üretir. " +
        "Hassas veriyi sohbetten İSTEME ve ASLA sohbete yazma — değerler yalnız tarayıcıdaki sayfada " +
        "görünür. Yanıt {url, expiresAt, message}; sohbete yalnız linki düşür.")]
    public static Task<FeatureObjectResultModel<RequestSensitiveLink.RequestSensitiveLinkResponse>>
        RequestSensitiveLinkAsync(
            [Description("Hassas verisi görüntülenecek/düzenlenecek merchant kimliği")] Guid merchantId,
            IMessageBus bus,
            IHttpContextAccessor http,
            CancellationToken ct)
    {
        var userId = CurrentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<RequestSensitiveLink.RequestSensitiveLinkResponse>>(
            new RequestSensitiveLink.RequestSensitiveLinkCommand(merchantId, userId), ct);
    }
}
