using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Merchant.Api.Domains.RegisterRequests.Features.Agents.Queries;

// 029 US3: agent yüzeyi — e-posta ile EN SON başvurunun durumu (case-insensitive, R4).
// Approved yanıtı MerchantId + MerchantKey döndürür (BİLİNÇLİ dev-açık karar — plan Complexity
// Tracking; redeem-link teslim modeli gelince bu alanlar kaldırılacak). Merchant document'ı
// aynı BC içinden okunur (cross-BC değil).
public static class RegistrationStatus
{
    public record RegistrationStatusQuery(string Email);

    public class RegistrationStatusResponse
    {
        public Guid RequestId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? RejectReason { get; set; }
        public Guid? MerchantId { get; set; }
        public string? MerchantKey { get; set; }
    }

    public class RegistrationStatusQueryHandler
    {
        public async Task<FeatureObjectResultModel<RegistrationStatusResponse>> Handle(
            RegistrationStatusQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var normalizedEmail = (query.Email ?? string.Empty).Trim().ToLowerInvariant();
            var request = await session.Query<RegisterRequest>()
                .Where(r => r.Email.ToLower() == normalizedEmail)
                .OrderByDescending(r => r.CreatedTime)
                .FirstOrDefaultAsync(ct);

            if (request is null)
                return FeatureObjectResultModel<RegistrationStatusResponse>.NotFound();

            var response = new RegistrationStatusResponse
            {
                RequestId = request.Id,
                Status = request.Status.ToString()
            };

            switch (request.Status)
            {
                case RegisterRequestStatus.Pending:
                    response.Message = "Başvurunuz gateway yöneticisinin onayını bekliyor.";
                    break;

                case RegisterRequestStatus.Rejected:
                    response.RejectReason = request.RejectReason;
                    response.Message = "Başvurunuz reddedildi. Dilerseniz aynı e-posta ile yeniden başvurabilirsiniz.";
                    break;

                case RegisterRequestStatus.Approved:
                    var merchant = await session.LoadAsync<Domains.Merchants.Merchant>(request.MerchantId!.Value, ct);
                    if (merchant is null)
                        return FeatureObjectResultModel<RegistrationStatusResponse>.NotFound();

                    response.MerchantId = merchant.Id;
                    response.MerchantKey = merchant.MerchantKey;
                    response.Message = "Başvurunuz onaylandı. MerchantId ve MerchantKey ile gateway'e " +
                                       "bağlanabilirsiniz; bu bilgileri yönetim panelinizdeki merchant " +
                                       "kimlik formuna kaydedin.";
                    break;
            }

            return FeatureObjectResultModel<RegistrationStatusResponse>.Ok(response);
        }
    }
}

// 029: MCP tool ince sarmalayıcıdır ve YALNIZ yukarıdaki slice'ı IMessageBus ile çağırır. Tool adı
// DIŞ SÖZLEŞMEDİR — ECommerce ChatAgent allowlist'i bu adı bekler (registration_status); değiştirme.
// Yüzey: /mcp, merchant.write.
/// <summary>US3 — e-posta ile başvuru durumu; Approved'da MerchantId + MerchantKey teslimi.</summary>
[McpServerToolType]
public static class RegistrationStatusMcpTool
{
    [McpServerTool(Name = "registration_status")]
    [Description("Verilen e-posta için EN SON başvurunun durumunu döner: Pending / Approved / Rejected " +
                 "(nedenle). Approved yanıtı merchantId + merchantKey içerir — yönetici bu ikiliyi kendi " +
                 "panelindeki merchant kimlik formuna kaydeder. On-demand sorgu; sürekli poll gerekmez.")]
    public static Task<FeatureObjectResultModel<RegistrationStatus.RegistrationStatusResponse>>
        RegistrationStatusAsync(
            [Description("Başvuruda kullanılan e-posta (büyük/küçük harf duyarsız)")] string email,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<RegistrationStatus.RegistrationStatusResponse>>(
            new RegistrationStatus.RegistrationStatusQuery(email), ct);
}
