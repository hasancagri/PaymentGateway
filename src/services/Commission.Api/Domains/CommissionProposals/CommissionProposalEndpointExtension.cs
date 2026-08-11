using Commission.Api.Domains.CommissionProposals.Features.Commands;
using Commission.Api.Domains.CommissionProposals.Features.Queries;

namespace Commission.Api.Domains.CommissionProposals;

/// <summary>
/// 019 — merchant'a dönük ANONİM karar uçları (contracts §2). Kimlik doğrulaması bilinçli YOK:
/// yetki biletin kendisidir (FR-004; tek kullanım + TTL + yalnız son teklif — aktivasyon redeem
/// emsali). Mini HTML sayfaları Türkçe; icra POST'tadır, GET yalnız onay/gerekçe formu gösterir.
/// </summary>
public static class CommissionProposalEndpointExtension
{
    /// <summary>Admin-düzlem sorgu uçları (yetkili) — US5 teklif durumu.</summary>
    public static void AddCommissionProposalGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/commission-proposals").WithTags("commission-proposals")
            .WithApiVersionSet(apiVersionSet)
            .GetCommissionProposalStatusGroupItemEndpoint();
    }

    public static void AddCommissionProposalDecisionEndpointExtension(this WebApplication app)
    {
        var group = app.MapGroup("commission-proposals/decision").WithTags("commission-proposal-decisions")
            .AllowAnonymous();

        group.MapGet("/{ticket}/accept", async (string ticket, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<GetProposalByTicket.GetProposalByTicketResponse>>(
                new GetProposalByTicket.GetProposalByTicketQuery(ticket), ct);

            if (!result.IsSuccess || !result.Data!.IsDecidable)
                return Html(InvalidTicketPage);

            return Html(Page("Komisyon Teklifini Kabul Et",
                $"<p>DropShop komisyon teklifi ({result.Data!.RowCount} satır) size mail ekindeki Excel dosyasıyla iletildi.</p>" +
                "<p>Kabul etmeniz halinde bu oranlar hesabınıza tanımlanır ve teklif değiştirilemez olur.</p>" +
                $"<form method=\"post\" action=\"/commission-proposals/decision/{ticket}/accept\">" +
                "<button type=\"submit\">Teklifi Kabul Ediyorum</button></form>"));
        });

        group.MapPost("/{ticket}/accept", async (string ticket, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<AcceptCommissionProposal.AcceptCommissionProposalResponse>>(
                new AcceptCommissionProposal.AcceptCommissionProposalCommand(ticket), ct);

            if (!result.IsSuccess)
                return Html(InvalidTicketPage);

            return Html(Page("Teklif Kabul Edildi",
                "<p>Komisyon teklifini kabul ettiniz; oranlar hesabınıza tanımlandı.</p>" +
                "<p>Hesabınızın aktivasyonu kendiliğinden tamamlanır — başka bir işlem gerekmez.</p>"));
        });

        group.MapGet("/{ticket}/reject", async (string ticket, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<GetProposalByTicket.GetProposalByTicketResponse>>(
                new GetProposalByTicket.GetProposalByTicketQuery(ticket), ct);

            if (!result.IsSuccess || !result.Data!.IsDecidable)
                return Html(InvalidTicketPage);

            return Html(Page("Komisyon Teklifini Reddet",
                "<p>Teklifi reddetme gerekçenizi yazın; itirazlarınız gateway ekibine iletilir ve " +
                "revize bir teklif hazırlanabilir.</p>" +
                $"<form method=\"post\" action=\"/commission-proposals/decision/{ticket}/reject\">" +
                "<textarea name=\"reason\" rows=\"8\" required " +
                "placeholder=\"Örn. 6 ve 9 taksit oranları yüksek; tek çekim kabul.\"></textarea>" +
                "<button type=\"submit\">Reddi Gönder</button></form>"));
        });

        group.MapPost("/{ticket}/reject", async (string ticket, [FromForm] string reason, IMessageBus bus, CancellationToken ct) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<RejectCommissionProposal.RejectCommissionProposalResponse>>(
                    new RejectCommissionProposal.RejectCommissionProposalCommand(ticket, reason), ct);

                if (!result.IsSuccess)
                    return Html(InvalidTicketPage);

                return Html(Page("Ret Kaydedildi",
                    "<p>Gerekçeniz kaydedildi ve gateway ekibine iletildi.</p>" +
                    "<p>Revize teklif hazırlanırsa yeni bir mail alacaksınız.</p>"));
            })
            // Anonim, bilet-yetkili dış form — antiforgery token'ı yok (bilinçli; yetki = bilet).
            .DisableAntiforgery();
    }

    private static IResult Html(string html) => Results.Content(html, "text/html; charset=utf-8");

    private static string Page(string title, string bodyHtml) =>
        "<!DOCTYPE html><html lang=\"tr\"><head><meta charset=\"utf-8\">" +
        $"<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>{title}</title>" +
        "<style>body{font-family:system-ui,sans-serif;max-width:34rem;margin:3rem auto;padding:0 1rem;color:#222}" +
        "h1{font-size:1.3rem}textarea{width:100%;box-sizing:border-box;margin:.5rem 0;font:inherit;padding:.5rem}" +
        "button{background:#1a7f37;color:#fff;border:0;padding:.6rem 1.2rem;border-radius:.35rem;font-size:1rem;cursor:pointer}" +
        "</style></head><body>" +
        $"<h1>{title}</h1>{bodyHtml}</body></html>";

    private static string InvalidTicketPage => Page("Geçersiz Bilet",
        "<p>Bu karar linki geçersiz: bilet kullanılmış, süresi dolmuş veya teklif güncellenmiş olabilir.</p>" +
        "<p>Güncel teklif için gateway ekibiyle iletişime geçin; yeni bir teklif maili gönderilebilir.</p>");
}