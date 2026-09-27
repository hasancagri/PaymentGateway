namespace Merchant.Api.Domains.Merchants.Features.Commands;

// 044 US2: hassas alanların (Email, GsmNumber, IdentityNumber, Iban, TaxNumber) admin
// düzlemindeki TEK yazma yüzeyi. Aggregate'in UpdateDetails'i tüm alanları tek imzada aldığından
// hassas-DIŞI alanlar MEVCUT değerlerden geçirilir (research R3); tip-koşullu kurallar
// (Individual→TCKN, Company→vergi) aynen çalışır. 048: BFF endpoint sarmalayıcı SÖKÜLDÜ — bu
// HANDLER hosted link token-endpoint'inden (SensitiveEntryEndpointExtension) IMessageBus ile çağrılır.
public static class UpdateMerchantSensitive
{
    public record UpdateMerchantSensitiveCommand(
        Guid MerchantId,
        string Email,
        string GsmNumber,
        string? IdentityNumber,
        string Iban,
        string? TaxNumber);

    public class UpdateMerchantSensitiveResponse
    {
        public Guid MerchantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string GsmNumber { get; set; } = string.Empty;
        public string? IdentityNumber { get; set; }
        public string Iban { get; set; } = string.Empty;
        public string? TaxNumber { get; set; }
    }

    [Transactional]
    public class UpdateMerchantSensitiveCommandHandler
    {
        public async Task<FeatureObjectResultModel<UpdateMerchantSensitiveResponse>> Handle(
            UpdateMerchantSensitiveCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(cmd.MerchantId, ct);
            if (merchant is null || merchant.IsDeleted)
                return FeatureObjectResultModel<UpdateMerchantSensitiveResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.MerchantId),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = merchant.UpdateDetails(
                merchant.Type, merchant.Name, cmd.Email, cmd.GsmNumber, merchant.Address, cmd.Iban,
                merchant.ContactName, merchant.ContactSurname,
                cmd.IdentityNumber, merchant.TaxOffice, cmd.TaxNumber, merchant.LegalCompanyTitle);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<UpdateMerchantSensitiveResponse>.Error(result.Messages);

            session.Store(merchant);

            return FeatureObjectResultModel<UpdateMerchantSensitiveResponse>.Ok(new UpdateMerchantSensitiveResponse
            {
                MerchantId = merchant.Id,
                Name = merchant.Name,
                Type = merchant.Type.ToString(),
                Email = merchant.Email,
                GsmNumber = merchant.GsmNumber,
                IdentityNumber = merchant.IdentityNumber,
                Iban = merchant.Iban,
                TaxNumber = merchant.TaxNumber
            });
        }
    }
}