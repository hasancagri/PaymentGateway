namespace Payment.Api.Domains.StoreOnboarding;

/// <summary>
/// 091: Store onboarding/aktivasyon yüzeyi. Sürüm segmentsiz (dış store kontratı sabit yol).
/// (1) POST /store-onboarding — EC başlatır (store-api-key). (2) GET /store-onboarding/{openToken} — hosted
/// form (anonim, gizli token). (3) POST /store-onboarding/{openToken}/submit — profil + fan-out (anonim).
/// (4) GET /store-onboarding/{pgStoreRef}/status — durum (store-api-key, credential'sız).
/// (5) POST /internal/onboarding/callback/{callbackToken} — banka→PG sonuç (anonim + token + HMAC).
/// (6) POST /store-onboarding/{pgStoreRef}/banks/{bankCode}/resubmit — reddedilen bankaya yeniden başvuru (store-api-key).
/// </summary>
public static class StoreOnboardingEndpointExtension
{
    public static void AddStoreOnboardingEndpointExtension(this WebApplication app)
    {
        // (1) EC → PG: aktivasyonu başlat. store-api-key; opak PgStoreRef + form link döner (sır içermez).
        app.MapPost("/store-onboarding", async ([FromBody] CreateBody body, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateStoreOnboarding.CreateStoreOnboardingResponse>>(
                    new CreateStoreOnboarding.CreateStoreOnboardingCommand(body.MerchantId, body.CorrelationId, body.StoreName));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithTags("store-onboarding")
            .RequireAuthorization(AuthorizationPolicies.StoreApiKey);

        // (2) Store → PG: hosted formu aç. Anonim, gizli OpenToken kapılı (fail-closed).
        app.MapGet("/store-onboarding/{openToken}", async (string openToken, IMessageBus bus, StoreOnboardingOptions options) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<OpenOnboardingForm.OpenOnboardingFormResponse>>(
                    new OpenOnboardingForm.OpenOnboardingFormCommand(openToken));
                if (!result.IsSuccess || result.Data is null) return Results.NotFound();
                return Results.Content(StoreOnboardingPages.BuildOnboardingFormPage(openToken, result.Data, options), "text/html");
            })
            .WithTags("store-onboarding")
            .AllowAnonymous();

        // (3) Store → PG: formu gönder → bankalara fan-out. Anonim, gizli OpenToken kapılı.
        app.MapPost("/store-onboarding/{openToken}/submit", async (string openToken, [FromBody] SubmitBody body, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<SubmitOnboardingProfile.SubmitOnboardingProfileResponse>>(
                    new SubmitOnboardingProfile.SubmitOnboardingProfileCommand(
                        openToken, body.LegalName, body.TaxNumber, body.Iban, body.Mcc,
                        body.WebsiteUrl, body.ContactName, body.ContactEmail));
                if (result.IsSuccess) return Results.Ok(result.Data);
                var code = result.Messages?.FirstOrDefault()?.Code;
                return code == StoreOnboardingResourceConstants.ONBOARDING_NOT_FOUND
                    ? Results.NotFound() : Results.BadRequest(result);
            })
            .WithTags("store-onboarding")
            .AllowAnonymous();

        // (4) EC → PG: aktivasyon durumu. store-api-key; credential/sır içermez.
        app.MapGet("/store-onboarding/{pgStoreRef}/status", async (string pgStoreRef, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<QueryOnboardingStatus.Response>>(
                    new QueryOnboardingStatus.Query(pgStoreRef));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound();
            })
            .WithTags("store-onboarding")
            .RequireAuthorization(AuthorizationPolicies.StoreApiKey);

        // (5) Banka → PG: onboarding sonucu (approve+credential / reject). Anonim; CallbackToken path kapısı +
        // X-Signature HMAC doğrulama (ham gövde). Credential yalnız bu imzalı gövdede akar (İlke VI makine-handoff).
        app.MapPost("/internal/onboarding/callback/{callbackToken}", async (
                string callbackToken, string bank, HttpRequest req, IMessageBus bus) =>
            {
                using var reader = new StreamReader(req.Body);
                var rawBody = await reader.ReadToEndAsync();
                var signature = req.Headers[ReceiveBankOnboardingOutcome.SignatureHeader].ToString();
                var result = await bus.InvokeAsync<FeatureResultModel>(
                    new ReceiveBankOnboardingOutcome.Receive(callbackToken, bank, rawBody, signature));
                // Token/imza eşleşmezse fail-closed (bilgi sızdırmaz).
                return result.IsSuccess ? Results.Ok() : Results.NotFound();
            })
            .WithTags("store-onboarding")
            .AllowAnonymous();

        // (6) EC → PG: reddedilen bankaya yeniden başvuru. store-api-key.
        app.MapPost("/store-onboarding/{pgStoreRef}/banks/{bankCode}/resubmit", async (
                string pgStoreRef, string bankCode, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureResultModel>(
                    new ResubmitBankApplication.Resubmit(pgStoreRef, bankCode));
                if (result.IsSuccess) return Results.Ok(result);
                var code = result.Messages?.FirstOrDefault()?.Code;
                return code == StoreOnboardingResourceConstants.ONBOARDING_NOT_FOUND
                    ? Results.NotFound() : Results.BadRequest(result);
            })
            .WithTags("store-onboarding")
            .RequireAuthorization(AuthorizationPolicies.StoreApiKey);
    }

    // EC başlatma gövdesi. MerchantId = store kimliği (CreatePaymentLink.MerchantId ile eşleşir).
    public record CreateBody(Guid MerchantId, string CorrelationId, string StoreName);

    // Hosted form gönderim gövdesi (çekirdek profil).
    public record SubmitBody(
        string LegalName, string TaxNumber, string Iban, string Mcc,
        string WebsiteUrl, string ContactName, string ContactEmail);
}
