namespace Merchant.Api.Domains.RegisterRequests.Features.Agents.Queries;

// 043 US2: yalnız Pending başvurular — operatörün karar bekleyen listesi. 044 FR-003: başvuru
// sahibinin kişisel/kimlik-belirleyen alanları (Email, GsmNumber, IdentityNumber, TaxNumber)
// sözleşmeden ÇIKARILDI — karar için başvuru Id + işletme adı/tipi + tarih yeterli. Agent slice
// kendi sorgusu (bilinçli tekrar).
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
        public string Address { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string ContactSurname { get; set; } = string.Empty;
        public string? TaxOffice { get; set; }
        public string? LegalCompanyTitle { get; set; }
        public DateTime CreatedTime { get; set; }
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
                        Address = r.Address,
                        ContactName = r.ContactName,
                        ContactSurname = r.ContactSurname,
                        TaxOffice = r.TaxOffice,
                        LegalCompanyTitle = r.LegalCompanyTitle,
                        CreatedTime = r.CreatedTime
                    }).ToList()
                });
        }
    }
}

/// <summary>US2 — bekleyen (Pending) kayıt başvurularının listesi; kişisel veri YOK (044).</summary>
[McpServerToolType]
public static class AdminGetPendingRegistrationsMcpTool
{
    [McpServerTool(Name = Shared.MerchantAdminTools.GetPendingRegistrations)]
    [Description("Admin onayı bekleyen (Pending) kayıt başvurularını döner — başvuru Id, işletme " +
                 "adı/tipi/adresi, iletişim adı ve tarih. Başvuru sahibinin kişisel verisi YANIT'TA YOK.")]
    public static Task<FeatureObjectResultModel<AdminGetPendingRegistrations.AdminGetPendingRegistrationsResponse>>
        AdminGetPendingRegistrationsAsync(IMessageBus bus, CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminGetPendingRegistrations.AdminGetPendingRegistrationsResponse>>(
            new AdminGetPendingRegistrations.AdminGetPendingRegistrationsQuery(), ct);
}
