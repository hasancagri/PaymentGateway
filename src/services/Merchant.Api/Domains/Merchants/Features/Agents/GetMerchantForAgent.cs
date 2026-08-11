
namespace Merchant.Api.Domains.Merchants.Features.Agents;

/// <summary>
/// US4 (agent yüzeyi) — merchant kimlik/iletişim/statü bilgisini agent'a açar. Agent slice'ları
/// Commands/Queries'e ASLA gitmez (bus ile bile); bu yüzden merchant + reference read-model okumasını
/// KENDİ İÇİNDE yapar (GetMerchant query'sinin kopyası — bilinçli tekrar, izolasyon için).
/// </summary>
public static class GetMerchantForAgent
{
    // 019: Name araması eklendi — agent "Kahve Dünyası'na teklif sun" akışında isimden id+email çözer
    // (contracts §4). MerchantId varsa o kazanır; yoksa Name (case-insensitive, önce tam eşleşme,
    // sonra tekil contains) kullanılır.
    public record GetMerchantForAgentQuery(Guid? MerchantId = null, string? Name = null);

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
        public string? MerchantMail { get; set; }
        public DateTime CreatedTime { get; set; }
    }

    public class GetMerchantForAgentQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetMerchantForAgentResponse>> Handle(
            GetMerchantForAgentQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            Merchant? merchant;
            if (query.MerchantId is { } merchantId && merchantId != Guid.Empty)
            {
                merchant = await session.Query<Merchant>()
                    .Where(m => m.Id == merchantId && !m.IsDeleted)
                    .FirstOrDefaultAsync(ct);
            }
            else if (!string.IsNullOrWhiteSpace(query.Name))
            {
                var needle = query.Name.Trim().ToLower();
                var candidates = await session.Query<Merchant>()
                    .Where(m => !m.IsDeleted && m.Name.ToLower().Contains(needle))
                    .ToListAsync(ct);

                // Tam eşleşme öncelikli; yoksa TEKİL contains — birden çok aday belirsizdir (duplicate).
                merchant = candidates.FirstOrDefault(m => string.Equals(m.Name, query.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                           ?? (candidates.Count == 1 ? candidates[0] : null);
                if (merchant is null && candidates.Count > 1)
                {
                    return FeatureObjectResultModel<GetMerchantForAgentResponse>.Error(new MessageItem
                    {
                        Property = "Name",
                        Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_DUPLICATE,
                        Params = candidates.Select(c => c.Name).Take(5).ToList()
                    });
                }
            }
            else
            {
                return FeatureObjectResultModel<GetMerchantForAgentResponse>.Error(new MessageItem
                {
                    Property = "MerchantId/Name",
                    Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
                });
            }

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
                MerchantMail = merchant.MerchantMail,
                CreatedTime = merchant.CreatedTime
            });
        }
    }
}