namespace Merchant.Api.Domains.RegisterRequests.Features.Agents.Commands;

// 029 US1: agent yüzeyi — MCP tool'u yalnız bu slice'ı çağırır (Commands/Queries'e gitmez,
// kendi command + handler'ını taşır). Mükerrer kontrolü burada (cross-document sorgu, aggregate'e
// girmez): aynı e-postada Pending varsa RECORD_DUPLICATE, Approved varsa INVALID_OPERATION_ERROR
// (zaten onaylı); Rejected yeniden başvuruyu ENGELLEMEZ (FR-003).
public static class SubmitRegistration
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
    public class SubmitRegistrationCommandHandler
    {
        public async Task<FeatureObjectResultModel<SubmitRegistrationResponse>> Handle(
            SubmitRegistrationCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            Merchant.Api.Options.AdminNotification adminNotification,
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

            // FR-012: kod (sözleşme) DEĞİŞMEZ — yalnız tool [Description]'ı istemci tarafında
            // "talepte bulunmuştunuz, onayı bekleniyor" anlamına gelecek şekilde netleştirir. Bu
            // dalda SendEmailRequested publish EDİLMEZ (yalnız yeni Pending kaydında mail gider).
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

            // FR-011: admin'e bilgilendirme maili — dormant Mail.Worker altyapısının İLK aktif
            // çağıranı ([Transactional] outbox: yalnız DB commit'te gider).
            await bus.PublishAsync(new Shared.IntegrationEvents.SendEmailRequested(
                adminNotification.AdminEmail,
                "PG'ye kayıt yaptırmak isteyen var",
                $"{request.Name} ({request.Email}) PG'ye kayıt başvurusu yaptı (requestId: {request.Id}). " +
                "Admin panelinden veya admin_get_pending_registrations ile inceleyip onaylayabilir/reddedebilirsiniz."));

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

// 029: MCP tool ince sarmalayıcıdır ve YALNIZ yukarıdaki slice'ı IMessageBus ile çağırır. Tool adı
// DIŞ SÖZLEŞMEDİR — ECommerce ChatAgent allowlist'i bu adı bekler (submit_registration); değiştirme.
// Yüzey: /mcp, merchant.write.
/// <summary>US1 — merchant kayıt başvurusu (023 alan seti; admin onayı bekler).</summary>
[McpServerToolType]
public static class SubmitRegistrationMcpTool
{
    [McpServerTool(Name = "submit_registration")]
    [Description("Merchant kayıt başvurusu açar: alanlar doğrulanır (tip-uyum matrisi, TR IBAN, " +
                 "e-posta) ve başvuru Pending (admin onayı bekler) olarak kaydolur; requestId döner. " +
                 "Kimlik/sır KABUL ETMEZ, merchant OLUŞTURMAZ. Aynı e-postayla bekleyen başvuru " +
                 "varken tekrar çağrılamaz — hata kodu COMMON_MESSAGE_RECORD_DUPLICATE ile döner; " +
                 "bu, kullanıcının DAHA ÖNCE talepte bulunduğu ve onayının hâlâ beklendiği anlamına " +
                 "gelir (yeni kayıt/mail oluşmaz).")]
    public static Task<FeatureObjectResultModel<SubmitRegistration.SubmitRegistrationResponse>>
        SubmitRegistrationAsync(
            [Description("İşyeri tipi: Personal | PrivateCompany | LimitedOrJointStockCompany")] string type,
            [Description("İşyeri/site adı")] string name,
            [Description("İletişim e-postası — başvuru kimliği; durum sorgusu bu adresle yapılır")] string email,
            [Description("Telefon (GSM)")] string gsmNumber,
            [Description("Adres")] string address,
            [Description("TR IBAN (mod-97 doğrulanır)")] string iban,
            [Description("Yetkili adı")] string contactName,
            [Description("Yetkili soyadı")] string contactSurname,
            IMessageBus bus,
            CancellationToken ct,
            [Description("TCKN — Personal ve PrivateCompany için zorunlu")] string? identityNumber = null,
            [Description("Vergi dairesi — PrivateCompany ve LimitedOrJointStockCompany için zorunlu")] string? taxOffice = null,
            [Description("Vergi no — LimitedOrJointStockCompany için zorunlu")] string? taxNumber = null,
            [Description("Ticari unvan — PrivateCompany ve LimitedOrJointStockCompany için zorunlu")] string? legalCompanyTitle = null)
        => bus.InvokeAsync<FeatureObjectResultModel<SubmitRegistration.SubmitRegistrationResponse>>(
            new SubmitRegistration.SubmitRegistrationCommand(
                type, name, email, gsmNumber, address, iban, contactName, contactSurname,
                identityNumber, taxOffice, taxNumber, legalCompanyTitle), ct);
}
