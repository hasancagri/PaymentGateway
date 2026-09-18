using System.ComponentModel;
using Common.Utils.Authorization;
using ModelContextProtocol.Server;

namespace Merchant.Api.Domains.RegisterRequests.Features.Agents.Commands;

// 043 US2: red — neden zorunlu, kayıtta saklanır. Agent slice kendi kopyası (mevcut
// RejectRegisterRequestCommandHandler ile aynı iş kuralı, bilinçli tekrar).
public static class AdminRejectRegistration
{
    [RequiredScope(AuthorizationScopes.MerchantAdmin)]
    public record AdminRejectRegistrationCommand(Guid RequestId, string Reason);

    public class AdminRejectRegistrationResponse
    {
        public Guid RequestId { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    [Transactional]
    public class AdminRejectRegistrationCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminRejectRegistrationResponse>> Handle(
            AdminRejectRegistrationCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var request = await session.LoadAsync<RegisterRequest>(cmd.RequestId, ct);
            if (request is null)
                return FeatureObjectResultModel<AdminRejectRegistrationResponse>.NotFound();

            var rejectResult = request.Reject(cmd.Reason);
            if (!rejectResult.IsSuccess)
                return FeatureObjectResultModel<AdminRejectRegistrationResponse>.Error(rejectResult.Messages);

            session.Store(request);

            return FeatureObjectResultModel<AdminRejectRegistrationResponse>.Ok(new AdminRejectRegistrationResponse
            {
                RequestId = request.Id,
                Status = request.Status.ToString()
            });
        }
    }
}

/// <summary>US2 — kayıt başvurusunu reddeder; yalnız Pending'den, neden zorunlu.</summary>
[McpServerToolType]
public static class AdminRejectRegistrationMcpTool
{
    [McpServerTool(Name = Shared.MerchantAdminTools.RejectRegistration)]
    [Description("Pending başvuruyu reddeder — neden zorunludur, başvuruda saklanır. Başvuru zaten " +
                 "terminal statüdeyse INVALID_OPERATION_ERROR döner; neden boşsa " +
                 "COMMON_MESSAGE_VALUE_IS_REQUIRED döner.")]
    public static Task<FeatureObjectResultModel<AdminRejectRegistration.AdminRejectRegistrationResponse>>
        AdminRejectRegistrationAsync(
            [Description("Reddedilecek başvuru kimliği")] Guid requestId,
            [Description("Red nedeni — zorunlu, başvuruda saklanır")] string reason,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminRejectRegistration.AdminRejectRegistrationResponse>>(
            new AdminRejectRegistration.AdminRejectRegistrationCommand(requestId, reason), ct);
}
