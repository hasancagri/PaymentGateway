using System.Collections.Concurrent;
using Merchant.Api.Utils;

namespace Merchant.Api;

// 048 D1/D5: hassas-veri hosted ekranı — tek sayfalık gömülü HTML (Razor/SPA yok). ANONİM:
// token = yetki (İLKE V capability-link istisnası; scope zorlaması link üretiminde). Bilinmeyen/
// tüketilmiş/süresi geçmiş token her iki uçta da NÖTR 404 (token doğruluğu sızdırılmaz — FR-008).
// GET tüketmez; POST başarısı Consume ile öldürür (D2). HTML/CSS Pages/SensitiveEntry/*.html gömülü
// resource'larında; C# yalnız okur (bir kez cache'ler) + {{placeholder}} doldurur + değerleri DAR
// encoder ile kaçırır (D4: framework HtmlEncoder.Default Türkçe/'+'yı bozardı) + servis eder.
public static class SensitiveEntryEndpointExtension
{
    private const string PageTitle = "Hassas Merchant Verisi";

    public static void MapSensitiveEntryEndpoints(this WebApplication app)
    {
        // GET: form, mevcut değerlerle dolu. Oturumu TÜKETMEZ (form açıp vazgeçmek linki öldürmez, süre öldürür).
        app.MapGet("/merchants/sensitive/{token}", async (
            string token, IQuerySession session, IMessageBus bus, CancellationToken ct) =>
        {
            var entry = await session.Query<Domains.Merchants.SensitiveEntrySession>()
                .FirstOrDefaultAsync(x => x.Token == token, ct);

            if (entry is null || !entry.IsUsable(DateTimeOffset.UtcNow))
                return Html(NotFoundPage(), StatusCodes.Status404NotFound);

            var sensitive = await bus.InvokeAsync<FeatureObjectResultModel<GetMerchantSensitive.GetMerchantSensitiveResponse>>(
                new GetMerchantSensitive.GetMerchantSensitiveQuery(entry.MerchantId), ct);

            // Merchant bulunamaz (silinmiş): nötr 404 — token doğruluğu sızdırılmaz.
            if (!sensitive.IsSuccess)
                return Html(NotFoundPage(), StatusCodes.Status404NotFound);

            return Html(ViewPage(token, sensitive.Data!, error: null), StatusCodes.Status200OK);
        });

        // POST: doğrula + kaydet. Başarı token'ı tüketir + PRG; doğrulama hatası formu YAŞATIR (düzeltilebilir).
        app.MapPost("/merchants/sensitive/{token}", async (
            string token, HttpRequest request, IDocumentSession session, IMessageBus bus, CancellationToken ct) =>
        {
            var entry = await session.Query<Domains.Merchants.SensitiveEntrySession>()
                .FirstOrDefaultAsync(x => x.Token == token, ct);

            if (entry is null || !entry.IsUsable(DateTimeOffset.UtcNow))
                return Html(NotFoundPage(), StatusCodes.Status404NotFound);

            var form = await request.ReadFormAsync(ct);
            var result = await bus.InvokeAsync<FeatureObjectResultModel<UpdateMerchantSensitive.UpdateMerchantSensitiveResponse>>(
                new UpdateMerchantSensitive.UpdateMerchantSensitiveCommand(
                    entry.MerchantId,
                    form["email"].ToString(),
                    form["gsmNumber"].ToString(),
                    NullIfEmpty(form["identityNumber"]),
                    form["iban"].ToString(),
                    NullIfEmpty(form["taxNumber"])), ct);

            if (result.IsSuccess)
            {
                var consume = entry.Consume(DateTimeOffset.UtcNow);
                if (consume.IsSuccess)
                    session.Store(entry);
                await session.SaveChangesAsync(ct);
                // PRG: başarı GET'e yönlendir (yenileme resubmit yapmaz). Token tüketildiğinden
                // hedef GET nötr 404 verir — success sayfası query flag ile ayrı gösterilir.
                return Results.Redirect($"/merchants/sensitive/{token}/saved");
            }

            var messages = result.Messages ?? [];
            if (messages.Any(m => m.Code == CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND))
                return Html(NotFoundPage(), StatusCodes.Status404NotFound);

            // Doğrulama hatası: form yeniden + hata, oturum YAŞAR (tüketilmez). Girilen değerler geri basılır.
            var entered = new GetMerchantSensitive.GetMerchantSensitiveResponse
            {
                Name = form["name"].ToString(),
                Email = form["email"].ToString(),
                GsmNumber = form["gsmNumber"].ToString(),
                IdentityNumber = form["identityNumber"].ToString(),
                Iban = form["iban"].ToString(),
                TaxNumber = form["taxNumber"].ToString()
            };
            return Html(ViewPage(token, entered, ErrorText), StatusCodes.Status400BadRequest);
        });

        // PRG hedefi: kayıt başarılıysa success sayfası (yenileme güvenli). Doğrudan erişilse de nötr success.
        app.MapGet("/merchants/sensitive/{token}/saved", () =>
            Html(SuccessPage(), StatusCodes.Status200OK));
    }

    private static string? NullIfEmpty(Microsoft.Extensions.Primitives.StringValues value)
        => string.IsNullOrWhiteSpace(value) ? null : value.ToString();

    private const string ErrorText =
        "Alanları kontrol edin: e-posta, telefon ve IBAN zorunlu; TCKN/vergi no işyeri tipine göre " +
        "gereklidir; IBAN TR biçiminde ve geçerli olmalıdır.";

    private static IResult Html(string html, int statusCode) =>
        Results.Content(html, "text/html; charset=utf-8", statusCode: statusCode);

    // Gömülü resource'u bir kez oku + cache'le. Ad = "<default-namespace>.<klasör noktalı>.<dosya>".
    private static readonly Assembly ResourceAssembly = typeof(SensitiveEntryEndpointExtension).Assembly;
    private static readonly ConcurrentDictionary<string, string> Templates = new();

    private static string Template(string name) => Templates.GetOrAdd(name, static key =>
    {
        var resourceName = $"Merchant.Api.Pages.SensitiveEntry.{key}";
        using var stream = ResourceAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Gömülü resource bulunamadı: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    // Layout: title encode edilir (çıktı korunur), body RAW (fragment o HTML olarak basılır).
    private static string Layout(string body) => Template("layout.html")
        .Replace("{{title}}", SensitiveHtmlEncoder.Encode(PageTitle))
        .Replace("{{body}}", body);

    // Her merchant değeri DAR encoder'dan geçer; TIRNAKLI attribute'a (value="{{...}}") basılır.
    private static string ViewPage(string token, GetMerchantSensitive.GetMerchantSensitiveResponse data, string? error) =>
        Layout(Template("view.html")
            .Replace("{{token}}", SensitiveHtmlEncoder.Encode(token))
            .Replace("{{name}}", SensitiveHtmlEncoder.Encode(data.Name))
            .Replace("{{email}}", SensitiveHtmlEncoder.Encode(data.Email))
            .Replace("{{gsm}}", SensitiveHtmlEncoder.Encode(data.GsmNumber))
            .Replace("{{identity}}", SensitiveHtmlEncoder.Encode(data.IdentityNumber ?? string.Empty))
            .Replace("{{iban}}", SensitiveHtmlEncoder.Encode(data.Iban))
            .Replace("{{tax}}", SensitiveHtmlEncoder.Encode(data.TaxNumber ?? string.Empty))
            .Replace("{{error}}", error is null ? "" : $"""<div class="error">{SensitiveHtmlEncoder.Encode(error)}</div>"""));

    private static string SuccessPage() => Layout(Template("success.html"));

    // Kendi doctype'lı bağımsız nötr sayfa (Layout kullanmaz).
    private static string NotFoundPage() => Template("notfound.html");
}
