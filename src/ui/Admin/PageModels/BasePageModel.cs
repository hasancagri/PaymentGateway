using Admin.Clients;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Admin.PageModels;

public abstract class BasePageModel : PageModel
{
    /// <summary>Bu istekte gösterilecek API doğrulama/hata mesajları (Türkçe).</summary>
    public List<string> Errors { get; } = new();

    /// <summary>Başarı bildirimi (redirect sonrası da taşınır).</summary>
    [TempData] public string? Flash { get; set; }

    protected void AddErrors(IEnumerable<ApiMessage> messages)
    {
        foreach (var text in MessageText.All(messages))
            Errors.Add(text);
    }
}