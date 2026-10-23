namespace Payment.Api.Domains.PaymentLinks;

/// <summary>
/// 089: PaymentLink (pay-by-link) yüzeyi. Sürüm segmentsiz (dış store kontratı sabit yol bekler).
/// (1) POST /payment-links — store başlatma (payment.write, GEÇİCİ yetki). (2) GET /payment-links/{openToken}
/// — müşteri açılışı + güven bağlamı (anonim, gizli token kapılı). (3) POST /payment-links/{openToken}/pay —
/// "Öde" NonSecure çekim, kart gövdede (anonim). (4) POST /payment-links/{pgPaymentRef}/cancel — store iptal (payment.write).
/// <para><b>Yetki (088/089 GEÇİCİ):</b> (1)+(4) payment.write JWT scope; tenant MerchantId gövdeden. Nihai
/// (sonraki iş): store→gateway AgentPlatform platform IdP + PG scope; müşteri yüzeyi anonim per-session
/// secret token (anayasa İlke V). payment.write placeholder, nihai yetki DEĞİL.</para>
/// </summary>
public static class PaymentLinkEndpointExtension
{
    public static void AddPaymentLinkEndpointExtension(this WebApplication app)
    {
        // (1) Store → PG: ödeme linki üret. GEÇİCİ payment.write; tenant MerchantId gövdeden.
        app.MapPost("/payment-links", async ([FromBody] CreatePaymentLinkBody body, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreatePaymentLink.CreatePaymentLinkResponse>>(
                    new CreatePaymentLink.CreatePaymentLinkCommand(
                        body.MerchantId, body.Amount, body.Currency, body.OrderRef,
                        body.ReturnUrl, body.StoreName, body.OrderSummary));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreatePaymentLink")
            .WithTags("payment-links")
            .RequireAuthorization(AuthorizationPolicies.StoreApiKey);

        // (2) Müşteri → PG: linki aç + güven bağlamı (kart formundan ÖNCE). Anonim, gizli token kapılı.
        app.MapGet("/payment-links/{openToken}", async (
                string openToken, IMessageBus bus, PaymentLinkOptions options) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<OpenPaymentLink.OpenPaymentLinkResponse>>(
                    new OpenPaymentLink.OpenPaymentLinkCommand(openToken));

                // Bilinmeyen/geçersiz/expired/cancelled → sayfa açılmaz (fail-closed, FR-007).
                if (!result.IsSuccess || result.Data is null)
                    return Results.NotFound();

                return Results.Content(PaymentLinkPages.BuildTrustContextPage(openToken, result.Data, options), "text/html");
            })
            .WithName("OpenPaymentLink")
            .WithTags("payment-links")
            .AllowAnonymous();

        // (3) Müşteri → PG: "Öde" (NonSecure çekim). Anonim, gizli token kapılı. Kart gövdede, yalnız çekime
        // iletilir — SAKLANMAZ/LOGLANMAZ (İlke VI; komut InvokeAsync-only → PAN outbox'a girmez). ASP.NET
        // varsayılanı istek gövdesini loglamaz; eklenecek request-logging bu ucun gövdesini ATLAMALIDIR.
        app.MapPost("/payment-links/{openToken}/pay", async (string openToken, [FromBody] PayCardBody body, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<PayPaymentLink.PayPaymentLinkResponse>>(
                    new PayPaymentLink.PayPaymentLinkCommand(
                        openToken,
                        new CardData(body.CardNumber, body.HolderName, body.ExpiryMonth, body.ExpiryYear, body.Cvv),
                        body.InstallmentCount));

                if (result.IsSuccess) return Results.Ok(result.Data);
                var code = result.Messages?.FirstOrDefault()?.Code;
                return code == PaymentLinkResourceConstants.LINK_NOT_FOUND
                    ? Results.NotFound()
                    : Results.BadRequest(result);
            })
            .WithName("PayPaymentLink")
            .WithTags("payment-links")
            .AllowAnonymous();

        // (3a') Müşteri → PG: BIN girince taksit seçenekleri (093). Her taksit için en-avantajlı banka + taksitli
        // toplam/aylık. Anonim, gizli token kapılı. BIN prefix (tam PAN değil — İlke VI). Banka adı ekranda gösterilir.
        app.MapGet("/payment-links/{openToken}/installments", async (string openToken, string bin, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<QuoteInstallments.Response>>(
                    new QuoteInstallments.Query(openToken, bin));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound();
            })
            .WithName("QuoteInstallments")
            .WithTags("payment-links")
            .AllowAnonymous();

        // (3b) Müşteri → PG: "3D ile öde". Link açılır + banka 3D gateway'ine gidecek İMZALI form render edilir.
        // Kart alanlarını TARAYICI ekler ve doğrudan bankaya POST eder — PG sunucusu PAN'ı görmez (İlke VI).
        app.MapGet("/payment-links/{openToken}/3d", async (string openToken, HttpRequest req, IMessageBus bus) =>
            {
                var callbackUrl = $"{req.Scheme}://{req.Host}/payment-links/{openToken}/3d-callback";
                var result = await bus.InvokeAsync<FeatureObjectResultModel<Initiate3DPayment.Response>>(
                    new Initiate3DPayment.Command(openToken, callbackUrl));
                if (!result.IsSuccess || result.Data is null) return Results.NotFound();
                return Results.Content(PaymentLinkPages.Build3DFormPage(result.Data), "text/html");
            })
            .WithName("Initiate3DPayment").WithTags("payment-links").AllowAnonymous();

        // (3c) Banka → PG: ACS sonrası sonucu buraya auto-POST eder (form-urlencoded + HASH). PG imzayı doğrular,
        // 3DModel çekimini tamamlar, link'i terminalize eder, müşteriyi mağaza ReturnUrl'üne yönlendirir. Anonim
        // (banka kimliği imza + ?bank= ile; fail-closed). Kart verisi bu uca GELMEZ (yalnız MD/3DStatus).
        app.MapPost("/payment-links/{openToken}/3d-callback", async (
                string openToken, string bank, HttpRequest req, IMessageBus bus) =>
            {
                var form = await req.ReadFormAsync();
                var callback = form.ToDictionary(kv => kv.Key, kv => kv.Value.ToString(), StringComparer.Ordinal);
                var result = await bus.InvokeAsync<FeatureObjectResultModel<Complete3DPayment.Response>>(
                    new Complete3DPayment.Command(openToken, bank, callback));
                return result.IsSuccess && result.Data is not null
                    ? Results.Redirect(result.Data.RedirectUrl)
                    : Results.Content(PaymentLinkPages.Build3DResultPage(), "text/html");
            })
            .WithName("Complete3DPayment").WithTags("payment-links").AllowAnonymous();

        // (4) Store → PG: linki iptal et. GEÇİCİ payment.write.
        app.MapPost("/payment-links/{pgPaymentRef}/cancel", async (string pgPaymentRef, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureResultModel>(
                    new CancelPaymentLink.CancelPaymentLinkCommand(pgPaymentRef));

                if (result.IsSuccess) return Results.Ok(result);
                var code = result.Messages?.FirstOrDefault()?.Code;
                return code == PaymentLinkResourceConstants.LINK_NOT_FOUND
                    ? Results.NotFound()
                    : Results.BadRequest(result);
            })
            .WithName("CancelPaymentLink")
            .WithTags("payment-links")
            .RequireAuthorization(AuthorizationPolicies.StoreApiKey);

        // (5) Store → PG: iptal (Void, gün sonu öncesi). Bankaya VoidAsync. payment.write.
        app.MapPost("/payment-links/{pgPaymentRef}/void", async (string pgPaymentRef, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureResultModel>(new VoidPaymentLink.Command(pgPaymentRef));
                if (result.IsSuccess) return Results.Ok(result);
                var code = result.Messages?.FirstOrDefault()?.Code;
                return code == PaymentLinkResourceConstants.LINK_NOT_FOUND ? Results.NotFound() : Results.BadRequest(result);
            })
            .WithTags("payment-links").RequireAuthorization(AuthorizationPolicies.StoreApiKey);

        // (6) Store → PG: iade (Refund, gün sonu sonrası, kısmi). Bankaya RefundAsync. payment.write.
        app.MapPost("/payment-links/{pgPaymentRef}/refund", async (string pgPaymentRef, [FromBody] RefundBody body, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureResultModel>(new RefundPaymentLink.Command(pgPaymentRef, body.Amount));
                if (result.IsSuccess) return Results.Ok(result);
                var code = result.Messages?.FirstOrDefault()?.Code;
                return code == PaymentLinkResourceConstants.LINK_NOT_FOUND ? Results.NotFound() : Results.BadRequest(result);
            })
            .WithTags("payment-links").RequireAuthorization(AuthorizationPolicies.StoreApiKey);

        // (7) Store → PG: durum sorgu (banka otorite; in-doubt sonrası kesin cevap). payment.read.
        app.MapGet("/payment-links/{pgPaymentRef}/status", async (string pgPaymentRef, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<QueryPaymentStatus.Response>>(
                    new QueryPaymentStatus.Query(pgPaymentRef));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound();
            })
            .WithTags("payment-links").RequireAuthorization(AuthorizationPolicies.StoreApiKey);

        // (8) Ops → PG: gün-sonu mutabakatı tetikle (günlük cron çağırır). Mesaj Wolverine outbox'ında dayanıklı
        // → gerçek iş Process handler'da (banka operasyon-API pull + PG Paid karşılaştırma). GEÇİCİ payment.write.
        app.MapPost("/internal/reconcile", async (IMessageBus bus) =>
            {
                await bus.PublishAsync(new Process.EndOfDayReconciliation.Run(DateTime.UtcNow));
                return Results.Accepted();
            })
            .WithTags("payment-links").RequireAuthorization(AuthorizationPolicies.StoreApiKey);
    }

    // Store ödeme linki üretme gövdesi (contracts/pg-payment-link.md §1). MerchantId gövdede (GEÇİCİ — İlke V).
    public record CreatePaymentLinkBody(
        Guid MerchantId, decimal Amount, string Currency, string OrderRef,
        string ReturnUrl, string StoreName, string OrderSummary);

    // Müşteri "Öde" kart gövdesi (contracts/pay-endpoint.md). Transient — saklanmaz/loglanmaz (İlke VI).
    // 093: InstallmentCount = seçilen taksit (1 = peşin); sunucu acquirer+tutarı yeniden hesaplar (client'a güvenilmez).
    public record PayCardBody(
        string CardNumber, string HolderName, string ExpiryMonth, string ExpiryYear, string Cvv, int InstallmentCount = 1);

    // Store iade gövdesi (kısmi tutar).
    public record RefundBody(decimal Amount);
}
