using Shared;

namespace Commission.Api.Domains.Reference;

/// <summary>
/// Reference.Api banka kataloğunun yerel izdüşümü (yalnız code→ad; Country/City/MCC Commission'da
/// kullanılmaz). Davranışsız read-model satırı; Marten kimliği = Code (Program.cs). <c>Bank.Create</c>
/// ad türetmesi + banka varlık doğrulaması bu kopyadan yapılır — senkron dış bağımlılık yok.
/// </summary>
public class ReferenceBank
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// <c>ReferenceDataUpdated</c> tüketicisi. Yalnız <c>Kind == "Bank"</c> ile ilgilenir; idempotent
/// upsert (Marten kimliği = Code → aynı kod overwrite, at-least-once teslimde tekrar zararsız).
/// </summary>
public static class ReferenceEventHandler
{
    public static async Task Handle(
        IntegrationEvents.ReferenceDataUpdated message,
        IDocumentSession session,
        CancellationToken ct)
    {
        if (message.Kind != "Bank")
            return;

        foreach (var item in message.Items)
            session.Store(new ReferenceBank { Code = item.Code, Name = item.Name });

        await session.SaveChangesAsync(ct);
    }
}