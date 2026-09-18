using Merchant.Api.Domains.OnboardingFormSessions.Features.Commands;

namespace Merchant.Api.Domains.OnboardingFormSessions;

// 045 US1: hosted başvuru formu — gömülü HTML (Payment.Api 041 dönüş sayfası emsali; Razor yok).
// Sayfalar ANONİM: token = yetki (sunucu-durumlu, süreli, tek başvuruluk). Bilinmeyen/ölü token
// her uçta nötr sayfa. S2S uçları OnboardingEndpointExtension grubunda (Program.cs).
public static class OnboardingFormSessionEndpointExtension
{
    public static void MapOnboardingFormPages(this WebApplication app)
    {
        // GET: form. Oturumu TÜKETMEZ (vazgeçmek linki öldürmez, süre öldürür).
        app.MapGet("/onboarding/form/{token}", async (
            string token, IQuerySession session, CancellationToken ct) =>
        {
            var formSession = await session.Query<OnboardingFormSession>()
                .FirstOrDefaultAsync(x => x.Token == token, ct);

            return formSession is not null && formSession.IsUsable(DateTimeOffset.UtcNow)
                ? Html(FormPage(token, formSession.Email, error: null), StatusCodes.Status200OK)
                : Html(NotFoundPage, StatusCodes.Status404NotFound);
        });

        // POST: başvuru. Doğrulama hatası formu hatayla yeniden gösterir (oturum yaşar).
        app.MapPost("/onboarding/form/{token}", async (
            string token, HttpRequest request, IQuerySession querySession, IMessageBus bus, CancellationToken ct) =>
        {
            var form = await request.ReadFormAsync(ct);
            var result = await bus.InvokeAsync<FeatureObjectResultModel<SubmitOnboardingForm.SubmitOnboardingFormResponse>>(
                new SubmitOnboardingForm.SubmitOnboardingFormCommand(
                    token,
                    form["type"].ToString(),
                    form["name"].ToString(),
                    form["gsmNumber"].ToString(),
                    form["address"].ToString(),
                    form["iban"].ToString(),
                    form["contactName"].ToString(),
                    form["contactSurname"].ToString(),
                    NullIfEmpty(form["identityNumber"]),
                    NullIfEmpty(form["taxOffice"]),
                    NullIfEmpty(form["taxNumber"]),
                    NullIfEmpty(form["legalCompanyTitle"])), ct);

            if (result.IsSuccess)
                return Html(SubmittedPage, StatusCodes.Status200OK);

            var messages = result.Messages ?? [];
            if (messages.Any(m => m.Code == CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND))
                return Html(NotFoundPage, StatusCodes.Status404NotFound);

            // Doğrulama/mükerrer hatası: form yeniden, oturum yaşar.
            var email = (await querySession.Query<OnboardingFormSession>()
                .FirstOrDefaultAsync(x => x.Token == token, ct))?.Email ?? string.Empty;
            return Html(FormPage(token, email, ErrorText(messages)), StatusCodes.Status400BadRequest);
        });
    }

    private static string? NullIfEmpty(Microsoft.Extensions.Primitives.StringValues value)
        => string.IsNullOrWhiteSpace(value) ? null : value.ToString();

    private static string ErrorText(List<MessageItem> messages)
    {
        if (messages.Any(m => m.Code == CommonResourceConstants.COMMON_MESSAGE_RECORD_DUPLICATE))
            return "Bu e-posta ile bekleyen bir başvuru zaten var; yeni başvuru açılamaz.";
        if (messages.Any(m => m.Code == CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR
                              && m.Property == "Email"))
            return "Bu e-posta ile onaylanmış bir kayıt zaten var.";

        var fields = string.Join(", ", messages.Select(m => FieldLabel(m.Property)).Distinct());
        return $"Alanları kontrol edin: {fields}. Zorunlu alanlar işyeri tipine göre değişir; " +
               "IBAN TR biçiminde ve geçerli olmalıdır.";
    }

    private static string FieldLabel(string? property) => property switch
    {
        "Name" => "işyeri adı",
        "GsmNumber" => "telefon",
        "Address" => "adres",
        "Iban" => "IBAN",
        "ContactName" => "yetkili adı",
        "ContactSurname" => "yetkili soyadı",
        "IdentityNumber" => "TCKN",
        "TaxOffice" => "vergi dairesi",
        "TaxNumber" => "vergi no",
        "LegalCompanyTitle" => "ticari unvan",
        "Type" => "işyeri tipi",
        _ => property ?? "form"
    };

    internal static IResult Html(string html, int statusCode) =>
        Results.Content(html, "text/html; charset=utf-8", statusCode: statusCode);

    internal static string Layout(string title, string body) => $$"""
        <!DOCTYPE html>
        <html lang="tr">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="robots" content="noindex, nofollow">
        <title>{{title}}</title>
        <style>
        body { font-family: -apple-system, Segoe UI, Roboto, sans-serif; background: #f5f6f8; margin: 0;
               display: flex; justify-content: center; padding: 40px 16px; }
        .card { background: #fff; border-radius: 12px; box-shadow: 0 2px 12px rgba(0,0,0,.08);
                padding: 32px; max-width: 520px; width: 100%; }
        h1 { font-size: 1.2rem; margin: 0 0 8px; }
        p, .hint { color: #555; font-size: .9rem; line-height: 1.5; }
        .hint { font-size: .78rem; margin: 2px 0 0; }
        label { display: block; font-size: .85rem; font-weight: 600; margin: 14px 0 4px; }
        input, select { width: 100%; box-sizing: border-box; padding: 9px 12px; border: 1px solid #ccd;
                border-radius: 8px; font-size: .95rem; }
        input[readonly] { background: #eef0f3; color: #667; }
        button { margin-top: 24px; width: 100%; padding: 12px; border: 0; border-radius: 8px;
                 background: #1a56db; color: #fff; font-size: 1rem; font-weight: 600; cursor: pointer; }
        .error { background: #fde8e8; color: #9b1c1c; border-radius: 8px; padding: 12px; font-size: .88rem; }
        .ok { background: #def7ec; color: #03543f; border-radius: 8px; padding: 12px; font-size: .9rem; }
        .secret { font-family: ui-monospace, monospace; background: #f3f4f6; border-radius: 8px;
                  padding: 10px 12px; word-break: break-all; margin: 6px 0 14px; font-size: .95rem; }
        </style>
        </head>
        <body><div class="card">{{body}}</div></body>
        </html>
        """;

    private static string FormPage(string token, string email, string? error) => Layout(
        "Merchant Kayıt Başvurusu", $"""
        <h1>Merchant Kayıt Başvurusu</h1>
        <p>Ödeme gateway'ine kayıt başvurusu. Bilgiler yalnız gateway'de saklanır; başvurunuz
        yönetici onayından sonra aktifleşir.</p>
        {(error is null ? "" : $"""<div class="error">{error}</div>""")}
        <form method="post" action="/onboarding/form/{token}" autocomplete="off">
          <label for="email">E-posta (başvuru kimliği)</label>
          <input id="email" value="{email}" readonly>
          <label for="type">İşyeri tipi</label>
          <select id="type" name="type" required>
            <option value="Personal">Personal (şahıs)</option>
            <option value="PrivateCompany">PrivateCompany (şahıs şirketi)</option>
            <option value="LimitedOrJointStockCompany">LimitedOrJointStockCompany (Ltd/A.Ş.)</option>
          </select>
          <label for="name">İşyeri/site adı</label>
          <input id="name" name="name" required>
          <label for="gsmNumber">Telefon (GSM)</label>
          <input id="gsmNumber" name="gsmNumber" required>
          <label for="address">Adres</label>
          <input id="address" name="address" required>
          <label for="iban">TR IBAN</label>
          <input id="iban" name="iban" required placeholder="TR...">
          <label for="contactName">Yetkili adı</label>
          <input id="contactName" name="contactName" required>
          <label for="contactSurname">Yetkili soyadı</label>
          <input id="contactSurname" name="contactSurname" required>
          <label for="identityNumber">TCKN</label>
          <input id="identityNumber" name="identityNumber">
          <p class="hint">Personal ve PrivateCompany için zorunlu.</p>
          <label for="taxOffice">Vergi dairesi</label>
          <input id="taxOffice" name="taxOffice">
          <p class="hint">PrivateCompany ve LimitedOrJointStockCompany için zorunlu.</p>
          <label for="taxNumber">Vergi no</label>
          <input id="taxNumber" name="taxNumber">
          <p class="hint">LimitedOrJointStockCompany için zorunlu.</p>
          <label for="legalCompanyTitle">Ticari unvan</label>
          <input id="legalCompanyTitle" name="legalCompanyTitle">
          <p class="hint">Şirket tipleri için zorunlu.</p>
          <button type="submit">Başvuruyu Gönder</button>
        </form>
        """);

    private static readonly string SubmittedPage = Layout("Başvuru Alındı", """
        <h1>Başvurunuz alındı ✓</h1>
        <div class="ok">Başvurunuz gateway yöneticisinin onayına sunuldu. Onaylandığında bu
        e-posta adresine erişim bilgilerinizi içeren bir bağlantı gönderilecek.</div>
        <p>Bu sayfayı kapatabilirsiniz; form bağlantısı artık geçersizdir.</p>
        """);

    internal static readonly string NotFoundPage = Layout("Sayfa bulunamadı", """
        <h1>Sayfa bulunamadı</h1>
        <p>Aradığınız bağlantı geçersiz ya da artık kullanılamıyor. Yeni bir bağlantı için
        başvuruyu başlatan tarafla iletişime geçin.</p>
        """);
}
