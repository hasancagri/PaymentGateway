namespace Merchant.Api.Domains.OnboardingFormSessions.Features.Commands;

// 045 US1 / kontrat #1: store S2S form oturumu açtırır (merchant.write makine kimliği). Aynı
// e-postada YAŞAYAN Pending başvuru varsa yeni oturum AÇILMAZ (formUrl:null + status); Approved
// da bloklar (zaten onaylı — 029 kuralı); Rejected yeniden başvuruyu engellemez.
public static class CreateFormSession
{
    public record CreateFormSessionCommand(string Email);

    public class CreateFormSessionResponse
    {
        public string? FormUrl { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public string ApplicationStatus { get; set; } = "None";
    }

    [Transactional]
    public class CreateFormSessionCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateFormSessionResponse>> Handle(
            CreateFormSessionCommand cmd,
            IDocumentSession session,
            Merchant.Api.Options.Onboarding onboarding,
            CancellationToken ct)
        {
            var normalizedEmail = (cmd.Email ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedEmail))
                return FeatureObjectResultModel<CreateFormSessionResponse>.Error(new MessageItem
                { Property = nameof(cmd.Email), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });

            var statuses = await session.Query<RegisterRequest>()
                .Where(r => r.Email.ToLower() == normalizedEmail)
                .Select(r => r.Status)
                .ToListAsync(ct);

            // Pending/Approved bloklar — store durum bilgisini alır, form linki üretilmez.
            if (statuses.Contains(RegisterRequestStatus.Pending))
                return FeatureObjectResultModel<CreateFormSessionResponse>.Ok(new CreateFormSessionResponse
                { FormUrl = null, ExpiresAt = null, ApplicationStatus = nameof(RegisterRequestStatus.Pending) });
            if (statuses.Contains(RegisterRequestStatus.Approved))
                return FeatureObjectResultModel<CreateFormSessionResponse>.Ok(new CreateFormSessionResponse
                { FormUrl = null, ExpiresAt = null, ApplicationStatus = nameof(RegisterRequestStatus.Approved) });

            var created = OnboardingFormSession.Create(normalizedEmail, onboarding.FormLinkLifetime);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<CreateFormSessionResponse>.Error(created.Messages);

            var formSession = created.Data!;
            session.Store(formSession);

            return FeatureObjectResultModel<CreateFormSessionResponse>.Ok(new CreateFormSessionResponse
            {
                FormUrl = $"{onboarding.PublicBaseUrl.TrimEnd('/')}/onboarding/form/{formSession.Token}",
                ExpiresAt = formSession.ExpiresAt,
                ApplicationStatus = statuses.Contains(RegisterRequestStatus.Rejected)
                    ? nameof(RegisterRequestStatus.Rejected)
                    : "None"
            });
        }
    }

    public static RouteGroupBuilder CreateFormSessionGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/sessions",
                async ([FromBody] CreateFormSessionCommand command, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateFormSessionResponse>>(command);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("CreateOnboardingFormSession")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantWrite)
            .Produces<CreateFormSessionResponse>();

        return group;
    }
}
