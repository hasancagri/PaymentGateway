namespace Merchant.Api.Domains.Merchants.Features.Queries;

// 046 US3/FR-011: ad ya da e-posta → MerchantId çözümleme (D5 eşi). TAM eşleşme (case-insensitive);
// tek eşleşme → id; sıfır → NotFound; birden çok → belirsizlik (INVALID_OPERATION_ERROR). Reissue
// handler'ı çıplak GUID yokken bunu kullanır — belirsiz/yok ise key DEĞİŞMEZ.
public static class ResolveMerchantByName
{
    public record ResolveMerchantByNameQuery(string? Name, string? Email);

    public class ResolveMerchantByNameResponse
    {
        public Guid MerchantId { get; set; }
    }

    public class ResolveMerchantByNameQueryHandler
    {
        public async Task<FeatureObjectResultModel<ResolveMerchantByNameResponse>> Handle(
            ResolveMerchantByNameQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var name = query.Name?.Trim();
            var email = query.Email?.Trim();
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(email))
                return FeatureObjectResultModel<ResolveMerchantByNameResponse>.Error(new MessageItem
                { Property = nameof(query.Name), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });

            var candidates = session.Query<Merchant>().Where(m => !m.IsDeleted);
            candidates = !string.IsNullOrWhiteSpace(email)
                ? candidates.Where(m => m.Email.ToLower() == email!.ToLower())
                : candidates.Where(m => m.Name.ToLower() == name!.ToLower());

            var matches = await candidates.Select(m => m.Id).Take(2).ToListAsync(ct);

            if (matches.Count == 0)
                return FeatureObjectResultModel<ResolveMerchantByNameResponse>.NotFound();
            if (matches.Count > 1)
                // Belirsizlik: birden çok eşleşme → yanlış merchant'a dokunma (FR-011).
                return FeatureObjectResultModel<ResolveMerchantByNameResponse>.Error(new MessageItem
                { Property = nameof(query.Name), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });

            return FeatureObjectResultModel<ResolveMerchantByNameResponse>.Ok(
                new ResolveMerchantByNameResponse { MerchantId = matches[0] });
        }
    }
}
