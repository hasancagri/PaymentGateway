using System.Diagnostics;
using Admin.PageModels;

namespace Admin.Pages;

public class ErrorModel : BasePageModel
{
    public string? RequestId { get; set; }

    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    }
}