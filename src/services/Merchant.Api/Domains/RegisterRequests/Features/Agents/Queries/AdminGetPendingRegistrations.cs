using System.ComponentModel;
using Common.Utils.Authorization;
using ModelContextProtocol.Server;

namespace Merchant.Api.Domains.RegisterRequests.Features.Agents.Queries;

// 043 US2: yalnız Pending başvurular — operatörün karar bekleyen listesi. Agent slice kendi
// sorgusu (mevcut ListRegisterRequests REST query'siyle KOD PAYLAŞMAZ, bilinçli tekrar).
public static class AdminGetPendingRegistrations
{
    [RequiredScope(AuthorizationScopes.MerchantAdmin)]
    public record AdminGetPendingRegistrationsQuery;

    public class AdminGetPendingRegistrationsResponse
    {
        public List<PendingRegistrationItem> Requests { get; set; } = new();
    }

    public class PendingRegistrationItem
    {
        public Guid RequestId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string GsmNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string ContactSurname { get; set; } = string.Empty;
        public string? IdentityNumber { get; set; }
        public string? TaxOffice { get; set; }
        public string? TaxNumber { get; set; }
        public string? LegalCompanyTitle { get; set; }
    }

    public class AdminGetPendingRegistrationsQueryHandler
    {
        public async Task<FeatureObjectResultModel<AdminGetPendingRegistrationsResponse>> Handle(
            AdminGetPendingRegistrationsQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var requests = await session.Query<RegisterRequest>()
                .Where(r => r.Status == RegisterRequestStatus.Pending)
                .OrderByDescending(r => r.CreatedTime)
                .ToListAsync(ct);

            return FeatureObjectResultModel<AdminGetPendingRegistrationsResponse>.Ok(
                new AdminGetPendingRegistrationsResponse
                {
                    Requests = requests.Select(r => new PendingRegistrationItem
                    {
                        RequestId = r.Id,
                        Type = r.Type.ToString(),
                        Name = r.Name,
                        Email = r.Email,
                        GsmNumber = r.GsmNumber,
                        Address = r.Address,
                        ContactName = r.ContactName,
                        ContactSurname = r.ContactSurname,
                        IdentityNumber = r.IdentityNumber,
                        TaxOffice = r.TaxOffice,
                        TaxNumber = r.TaxNumber,
                        LegalCompanyTitle = r.LegalCompanyTitle
                    }).ToList()
                });
        }
    }
}

/// <summary>US2 — bekleyen (Pending) kayıt başvurularının listesi, karar için KYC-benzeri alanlarla.</summary>
[McpServerToolType]
public static class AdminGetPendingRegistrationsMcpTool
{
    [McpServerTool(Name = Shared.MerchantAdminTools.GetPendingRegistrations)]
    [Description("Admin onayı bekleyen (Pending) kayıt başvurularını döner — operatör kararı için " +
                 "gerekli alanlarla (kimlik/vergi bilgisi dahil, sır DEĞİL).")]
    public static Task<FeatureObjectResultModel<AdminGetPendingRegistrations.AdminGetPendingRegistrationsResponse>>
        AdminGetPendingRegistrationsAsync(IMessageBus bus, CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminGetPendingRegistrations.AdminGetPendingRegistrationsResponse>>(
            new AdminGetPendingRegistrations.AdminGetPendingRegistrationsQuery(), ct);
}
