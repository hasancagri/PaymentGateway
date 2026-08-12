using System.Text.Json;
using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.AgentChat;

/// <summary>
/// 019 komisyon pazarlık chat ekranı: admin, Merchant.Agent ile metinle konuşur ("teklif sun",
/// "satır 37'yi 1.85 yap", "merchant'a gönder"...). Geçmiş + contextId form üzerinde taşınır
/// (sunucu state'i yok — dev aracı; kalıcı ekran 032/G3 admin persona işinde).
/// </summary>
public class IndexModel : BasePageModel
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IMerchantAgentClient _agent;

    public IndexModel(IMerchantAgentClient agent) => _agent = agent;

    [BindProperty]
    public string? Input { get; set; }

    [BindProperty]
    public string? ContextId { get; set; }

    /// <summary>Konuşma geçmişi — hidden alanda JSON taşınır (round-trip).</summary>
    [BindProperty]
    public string? TranscriptJson { get; set; }

    public List<ChatLine> Transcript { get; private set; } = new();

    public void OnGet()
    {
    }

    public async Task OnPostAsync(CancellationToken ct)
    {
        Transcript = ReadTranscript();

        if (string.IsNullOrWhiteSpace(Input))
        {
            Errors.Add("Mesaj boş olamaz.");
            return;
        }

        Transcript.Add(new ChatLine("admin", Input.Trim()));

        var result = await _agent.SendAsync(Input.Trim(), ContextId, ct);
        if (result.IsSuccess)
        {
            Transcript.Add(new ChatLine("agent", result.Reply));
            ContextId = result.ContextId;
        }
        else
        {
            Errors.Add(result.Reply);
        }

        TranscriptJson = JsonSerializer.Serialize(Transcript, Json);
        Input = string.Empty;
    }

    private List<ChatLine> ReadTranscript()
    {
        if (string.IsNullOrWhiteSpace(TranscriptJson))
            return new List<ChatLine>();

        try
        {
            return JsonSerializer.Deserialize<List<ChatLine>>(TranscriptJson, Json) ?? new List<ChatLine>();
        }
        catch
        {
            return new List<ChatLine>();
        }
    }

    public record ChatLine(string Role, string Text);
}
