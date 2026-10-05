using System.Security.Cryptography;
using System.Text;
using Merchant.Api.Options;

namespace Merchant.Api.Domains.RegisterRequests.Features.Commands;

// 087 US1: store-başlatan S2S kayıt ucu — eski hosted form-submit'in yerine (form SÖKÜLDÜ). Store
// finansal/PII'yi yalnız S2S gövdede taşır (MCP arg DEĞİL); correlationId + callbackUrl register'da
// saklanır (onayda credential callback'i bununla eşlenir). Auth iki katman: m2m bearer (merchant.write,
// taşıma kimliği) + X-Registration-Key (bootstrap sır, kayıt-ucu yetkisi) endpoint'te. İdempotency
// (aynı correlationId / mükerrer e-posta) handler'da cross-document sorguyla. Kontrat: contracts/register-endpoint.md.
public static class SubmitRegistration
{
    public record SubmitRegistrationCommand(
        Guid CorrelationId,
        string CallbackUrl,
        MerchantType Type,
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
        public bool Accepted { get; set; }
        public Guid CorrelationId { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    [Transactional]
    public class SubmitRegistrationCommandHandler
    {
        public async Task<FeatureObjectResultModel<SubmitRegistrationResponse>> Handle(
            SubmitRegistrationCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            // İdempotency: aynı correlationId zaten varsa yeni kayıt YARATMA (çift başvuru guard).
            var existingByCorrelation = await session.Query<RegisterRequest>()
                .Where(r => r.CorrelationId == cmd.CorrelationId)
                .FirstOrDefaultAsync(ct);
            if (existingByCorrelation is not null)
                return FeatureObjectResultModel<SubmitRegistrationResponse>.Error(new MessageItem
                { Property = nameof(RegisterRequest.CorrelationId), Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_DUPLICATE });

            // Mükerrer e-posta: bekleyen/onaylı başvuru varsa yeni başvuru açılmaz (case-insensitive).
            var email = cmd.Email?.Trim() ?? string.Empty;
            var duplicateEmail = await session.Query<RegisterRequest>()
                .Where(r => r.Email.ToLower() == email.ToLower()
                            && (r.Status == RegisterRequestStatus.Pending || r.Status == RegisterRequestStatus.Approved))
                .AnyAsync(ct);
            if (duplicateEmail)
                return FeatureObjectResultModel<SubmitRegistrationResponse>.Error(new MessageItem
                { Property = nameof(RegisterRequest.Email), Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_DUPLICATE });

            var submit = RegisterRequest.Submit(
                cmd.Type, cmd.Name, cmd.Email, cmd.GsmNumber, cmd.Address, cmd.Iban,
                cmd.ContactName, cmd.ContactSurname, cmd.IdentityNumber, cmd.TaxOffice,
                cmd.TaxNumber, cmd.LegalCompanyTitle, cmd.CorrelationId, cmd.CallbackUrl);
            if (!submit.IsSuccess)
                return FeatureObjectResultModel<SubmitRegistrationResponse>.Error(submit.Messages);

            session.Store(submit.Data!);

            return FeatureObjectResultModel<SubmitRegistrationResponse>.Ok(new SubmitRegistrationResponse
            {
                Accepted = true,
                CorrelationId = cmd.CorrelationId,
                Status = submit.Data!.Status.ToString()
            });
        }
    }
}

public static class SubmitRegistrationEndpoint
{
    public const string RegistrationKeyHeader = "X-Registration-Key";

    // S2S REST (store → PG): POST api/v1/onboarding/register. Auth = ecommerce-onboarding m2m
    // (merchant.write — taşıma kimliği) + X-Registration-Key (bootstrap sır — kayıt-ucu yetkisi).
    // Credential (MerchantId/Key) bu yanıtta DÖNMEZ — onay asenkron (admin MCP), callback'le gelir.
    public static RouteGroupBuilder SubmitRegistrationGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/register",
                async ([FromBody] RegisterRequestBody body, HttpRequest http,
                    OnboardingCallbackOptions options, IMessageBus bus) =>
                {
                    // Bootstrap key constant-time doğrulama (fail-closed; geçersiz/eksik → 401).
                    var provided = http.Headers[RegistrationKeyHeader].ToString();
                    if (!FixedTimeEquals(provided, options.BootstrapRegistrationKey))
                        return Results.Unauthorized();

                    var result = await bus.InvokeAsync<FeatureObjectResultModel<SubmitRegistration.SubmitRegistrationResponse>>(
                        new SubmitRegistration.SubmitRegistrationCommand(
                            body.CorrelationId, body.CallbackUrl, body.Business.Type, body.Business.Name,
                            body.Business.Email, body.Business.GsmNumber, body.Business.Address, body.Business.Iban,
                            body.Business.ContactName, body.Business.ContactSurname, body.Business.IdentityNumber,
                            body.Business.TaxOffice, body.Business.TaxNumber, body.Business.LegalCompanyTitle));

                    if (result.IsSuccess)
                        return Results.Accepted(value: result.Data);

                    // Mükerrer (correlationId/e-posta) → 409 idempotent; diğer doğrulama → 400.
                    var duplicate = (result.Messages ?? [])
                        .Any(m => m.Code == CommonResourceConstants.COMMON_MESSAGE_RECORD_DUPLICATE);
                    return duplicate ? Results.Conflict(result) : Results.BadRequest(result);
                })
            .WithName("SubmitRegistration")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.MerchantWrite)
            .Produces<SubmitRegistration.SubmitRegistrationResponse>(StatusCodes.Status202Accepted)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        return group;
    }

    // Ham bayt üstünde sabit-zaman karşılaştırma (uzunluk sızıntısı hariç, timing-safe).
    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    // S2S gövde (contracts/register-endpoint.md): correlationId + callbackUrl + nested business.
    public record RegisterRequestBody(Guid CorrelationId, string CallbackUrl, BusinessBody Business);

    public record BusinessBody(
        MerchantType Type,
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
}