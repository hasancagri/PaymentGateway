
namespace Merchant.Api.Domains.Merchants.Features.Agents;

/// <summary>
/// US4 (agent yüzeyi) — merchant kimlik/iletişim/statü bilgisini agent'a açar. Agent slice'ları
/// Commands/Queries'e ASLA gitmez (bus ile bile); bu yüzden merchant + reference read-model okumasını
/// KENDİ İÇİNDE yapar (GetMerchant query'sinin kopyası — bilinçli tekrar, izolasyon için).
/// </summary>
public static class GetMerchantForAgent
{
    public record GetMerchantForAgentQuery(Guid MerchantId);

    public class GetMerchantForAgentResponse
    {
        public Guid Id { get; set; }
        public string MerchantKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string CountryCode { get; set; } = string.Empty;
        public string? CountryName { get; set; }
        public string CityCode { get; set; } = string.Empty;
        public string? CityName { get; set; }
        public string Mcc { get; set; } = string.Empty;
        public string? MccName { get; set; }
        public string WebhookUrl { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? ReturnUrl { get; set; }
        public string? ExternalRef { get; set; }
        public DateTime CreatedTime { get; set; }
    }

    public class GetMerchantForAgentQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetMerchantForAgentResponse>> Handle(
            GetMerchantForAgentQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var merchant = await session.Query<Merchant>()
                .Where(m => m.Id == query.MerchantId && !m.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (merchant is null)
                return FeatureObjectResultModel<GetMerchantForAgentResponse>.NotFound();

            // İsim zenginleştirme: Reference olaylarıyla beslenen yerel read-model'den (id = Code).
            var country = await session.LoadAsync<ReferenceCountry>(ReferenceKey.Country(merchant.CountryCode), ct);
            var city = await session.LoadAsync<ReferenceCity>(merchant.CityCode, ct);
            var mcc = await session.LoadAsync<ReferenceMcc>(merchant.Mcc, ct);

            return FeatureObjectResultModel<GetMerchantForAgentResponse>.Ok(new GetMerchantForAgentResponse
            {
                Id = merchant.Id,
                MerchantKey = merchant.MerchantKey,
                Name = merchant.Name,
                Email = merchant.Email,
                Phone = merchant.Phone,
                CountryCode = merchant.CountryCode,
                CountryName = country?.Name,
                CityCode = merchant.CityCode,
                CityName = city?.Name,
                Mcc = merchant.Mcc,
                MccName = mcc?.Name,
                WebhookUrl = merchant.WebhookUrl,
                Status = merchant.Status.ToString(),
                ReturnUrl = merchant.ReturnUrl,
                ExternalRef = merchant.ExternalRef,
                CreatedTime = merchant.CreatedTime
            });
        }
    }
}