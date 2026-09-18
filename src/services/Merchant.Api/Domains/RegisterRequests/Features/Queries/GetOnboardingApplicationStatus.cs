namespace Merchant.Api.Domains.RegisterRequests.Features.Queries;

// 045 US3 / kontrat #2: store S2S durum sorgusu (merchant.read). EN SON başvurunun durumu;
// hiç başvuru yoksa 404 DEĞİL status:"None" (store'un dostane-mesaj üretimi basit kalsın).
// MerchantId/MerchantKey bu yanıtta ASLA yer almaz (029'dan fark — teslim yolu mail+sayfa).
public static class GetOnboardingApplicationStatus
{
    public record GetOnboardingApplicationStatusQuery(string Email);

    public class GetOnboardingApplicationStatusResponse
    {
        public string Status { get; set; } = "None";
        public string Message { get; set; } = string.Empty;
        public string? RejectReason { get; set; }
    }

    public class GetOnboardingApplicationStatusQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetOnboardingApplicationStatusResponse>> Handle(
            GetOnboardingApplicationStatusQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var normalizedEmail = (query.Email ?? string.Empty).Trim().ToLowerInvariant();
            var request = await session.Query<RegisterRequest>()
                .Where(r => r.Email.ToLower() == normalizedEmail)
                .OrderByDescending(r => r.CreatedTime)
                .FirstOrDefaultAsync(ct);

            var response = new GetOnboardingApplicationStatusResponse();
            switch (request?.Status)
            {
                case null:
                    response.Status = "None";
                    response.Message = "Bu e-posta ile bir başvuru bulunmuyor.";
                    break;

                case RegisterRequestStatus.Pending:
                    response.Status = nameof(RegisterRequestStatus.Pending);
                    response.Message = "Başvuru gateway yöneticisinin onayını bekliyor.";
                    break;

                case RegisterRequestStatus.Rejected:
                    response.Status = nameof(RegisterRequestStatus.Rejected);
                    response.RejectReason = request.RejectReason;
                    response.Message = "Başvuru reddedildi; aynı e-posta ile yeniden başvurulabilir.";
                    break;

                case RegisterRequestStatus.Approved:
                    response.Status = nameof(RegisterRequestStatus.Approved);
                    response.Message = "Başvuru onaylandı. Erişim bilgileri başvuru e-postasına " +
                                       "gönderilen tek kullanımlık bağlantıyla teslim edilir.";
                    break;
            }

            return FeatureObjectResultModel<GetOnboardingApplicationStatusResponse>.Ok(response);
        }
    }

    public static RouteGroupBuilder GetOnboardingApplicationStatusGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/applications/{email}",
                async (string email, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<GetOnboardingApplicationStatusResponse>>(
                        new GetOnboardingApplicationStatusQuery(email));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("GetOnboardingApplicationStatus")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantRead)
            .Produces<GetOnboardingApplicationStatusResponse>();

        return group;
    }
}
