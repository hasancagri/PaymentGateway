using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.BinCards;

public class ResolveModel : BasePageModel
{
    private readonly IBinCardApiClient _api;

    public ResolveModel(IBinCardApiClient api) => _api = api;

    [BindProperty(SupportsGet = true)] public string? Bin { get; set; }

    public BinCardDetail? Detail { get; private set; }

    /// <summary>Arama yapıldı mı (sonuç/mesaj gösterimi için).</summary>
    public bool Searched { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Bin))
            return; // ilk açılış — arama yok

        Searched = true;
        var bin = Bin.Trim();

        // İstemci-tarafı doğrulama: yalnız rakam, 6 ya da 8 hane. Geçersizse çağrı yapma (FR-005).
        if (!bin.All(char.IsDigit) || (bin.Length != 6 && bin.Length != 8))
        {
            Errors.Add("BIN yalnız rakam olmalı ve 6 ya da 8 haneli olmalı.");
            return;
        }

        var result = await _api.GetDetailAsync(bin, ct);
        if (result.IsSuccess)
            Detail = result.Data;
        else
            AddErrors(result.Messages);
    }
}