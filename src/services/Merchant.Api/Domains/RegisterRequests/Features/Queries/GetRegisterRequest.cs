namespace Merchant.Api.Domains.RegisterRequests.Features.Queries;

/// <summary>US2 — talep ayrıntısı (domain + descriptor özeti + karar bilgisi).</summary>
public static class GetRegisterRequest
{
    public record GetRegisterRequestQuery(Guid Id);

    public class GetRegisterRequestResponse
    {
        public Guid Id { get; set; }
        public string Domain { get; set; } = string.Empty;
        public string LegalName { get; set; } = string.Empty;
        public string TaxId { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string WebhookUrl { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? ReviewNote { get; set; }
        public Guid? CreatedMerchantId { get; set; }
        public string? MerchantMail { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime? ReviewedAtUtc { get; set; }
    }

    public class GetRegisterRequestQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetRegisterRequestResponse>> Handle(
            GetRegisterRequestQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var r = await session.LoadAsync<RegisterRequest>(query.Id, ct);
            if (r is null)
                return FeatureObjectResultModel<GetRegisterRequestResponse>.NotFound();

            return FeatureObjectResultModel<GetRegisterRequestResponse>.Ok(new GetRegisterRequestResponse
            {
                Id = r.Id,
                Domain = r.Domain,
                LegalName = r.LegalName,
                TaxId = r.TaxId,
                ContactEmail = r.ContactEmail,
                WebhookUrl = r.WebhookUrl,
                Status = r.Status.ToString(),
                ReviewNote = r.ReviewNote,
                CreatedMerchantId = r.CreatedMerchantId,
                MerchantMail = r.MerchantMail,
                CreatedTime = r.CreatedTime,
                ReviewedAtUtc = r.ReviewedAtUtc
            });
        }
    }
}
