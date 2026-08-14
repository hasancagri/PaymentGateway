namespace Merchant.Api.Domains.RegisterRequests.Features.Agents;

// 029 US1: agent yüzeyi — MCP tool'u yalnız bu slice'ı çağırır (015: Commands/Queries'e gitmez,
// kendi command + handler'ını taşır). Mükerrer kontrolü burada (cross-document sorgu, aggregate'e
// girmez): aynı e-postada Pending varsa RECORD_DUPLICATE, Approved varsa INVALID_OPERATION_ERROR
// (zaten onaylı); Rejected yeniden başvuruyu ENGELLEMEZ (FR-003).
public static class SubmitRegistrationForAgent
{
    public record SubmitRegistrationCommand(
        string Type,
        string Name,
        string Email,
        string GsmNumber,
        string Address,
        string Iban,
        string ContactName,
        string ContactSurname,
        string? IdentityNumber,
        string? TaxOffice,
        string? TaxNumber,
        string? LegalCompanyTitle);

    public class SubmitRegistrationResponse
    {
        public Guid RequestId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    [Transactional]
    public class SubmitRegistrationForAgentCommandHandler
    {
        public async Task<FeatureObjectResultModel<SubmitRegistrationResponse>> Handle(
            SubmitRegistrationCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            if (!Enum.TryParse<MerchantType>(cmd.Type?.Trim(), ignoreCase: true, out var type))
                return FeatureObjectResultModel<SubmitRegistrationResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Type),
                    Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
                });

            // Mükerrer kuralı e-posta üstünden, case-insensitive (R4/FR-003).
            var normalizedEmail = (cmd.Email ?? string.Empty).Trim().ToLowerInvariant();
            var existingStatuses = await session.Query<RegisterRequest>()
                .Where(r => r.Email.ToLower() == normalizedEmail)
                .Select(r => r.Status)
                .ToListAsync(ct);

            if (existingStatuses.Contains(RegisterRequestStatus.Pending))
                return FeatureObjectResultModel<SubmitRegistrationResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Email),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_DUPLICATE
                });
            if (existingStatuses.Contains(RegisterRequestStatus.Approved))
                return FeatureObjectResultModel<SubmitRegistrationResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Email),
                    Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR
                });

            var result = RegisterRequest.Submit(
                type, cmd.Name, cmd.Email, cmd.GsmNumber, cmd.Address, cmd.Iban,
                cmd.ContactName, cmd.ContactSurname,
                cmd.IdentityNumber, cmd.TaxOffice, cmd.TaxNumber, cmd.LegalCompanyTitle);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<SubmitRegistrationResponse>.Error(result.Messages);

            var request = result.Data!;
            session.Store(request);

            return FeatureObjectResultModel<SubmitRegistrationResponse>.Ok(new SubmitRegistrationResponse
            {
                RequestId = request.Id,
                Status = request.Status.ToString(),
                Message = "Başvurunuz alındı; gateway yöneticisinin onayı bekleniyor. " +
                          "Durumu registration_status aracıyla e-posta adresinizle sorgulayabilirsiniz."
            });
        }
    }
}
