using Merchant.Api.ReadModels;
using Shared;

namespace Merchant.Api;

/// <summary>
/// <c>ReferenceDataUpdated</c> tüketicisi (Wolverine assembly taramasıyla keşfedilir). Kind'e göre
/// yerel read-model'i idempotent upsert eder (Marten kimliği = Code → aynı kod overwrite; at-least-once
/// teslimde tekrar zararsız). Tam-set veya diff fark etmez; her kayıt Code anahtarıyla yazılır.
/// </summary>
public class MerchantEventHandlers
{
    public static async Task Handle(
        IntegrationEvents.ReferenceDataUpdated message,
        IDocumentSession session,
        CancellationToken ct)
    {
        foreach (var item in message.Items)
        {
            switch (message.Kind)
            {
                case "Country":
                    session.Store(new ReferenceCountry { Code = item.Code, Name = item.Name });
                    break;
                case "City":
                    session.Store(new ReferenceCity
                    {
                        Code = item.Code,
                        Name = item.Name,
                        CountryCode = item.CountryCode ?? string.Empty
                    });
                    break;
                case "Mcc":
                    session.Store(new ReferenceMcc { Code = item.Code, Name = item.Name });
                    break;
                case "Bank":
                    session.Store(new ReferenceBank { Code = item.Code, Name = item.Name });
                    break;
            }
        }

        await session.SaveChangesAsync(ct);
    }
}