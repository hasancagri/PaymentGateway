using Commission.Api.Domains.Banks;
using Commission.Api.Domains.Reference;

namespace Commission.Api.Domains.Banks.Features.Queries;

/// <summary>
/// Seçilebilir bankaların kataloğu (Code+Name) — Reference-beslemeli yerel read-model'den (tek kaynak).
/// <c>onlyAvailable=true</c> → zaten eklenmiş (`!IsDeleted`) bankaları eler; operatör yalnız henüz
/// eklenmemişleri görür.
/// </summary>
public static class GetBankCatalog
{
    public record GetBankCatalogQuery(bool OnlyAvailable);

    public class GetBankCatalogResponse
    {
        public List<CatalogItem> Items { get; set; } = new();
    }

    public class CatalogItem
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class GetBankCatalogQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetBankCatalogResponse>> Handle(
            GetBankCatalogQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var banks = await session.Query<ReferenceBank>().OrderBy(b => b.Code).ToListAsync(ct);
            var entries = banks.AsEnumerable();

            if (query.OnlyAvailable)
            {
                var existingCodes = await session.Query<Bank>()
                    .Where(b => !b.IsDeleted)
                    .Select(b => b.Code)
                    .ToListAsync(ct);

                var taken = existingCodes.ToHashSet();
                entries = entries.Where(e => !taken.Contains(e.Code));
            }

            return FeatureObjectResultModel<GetBankCatalogResponse>.Ok(new GetBankCatalogResponse
            {
                Items = entries
                    .Select(e => new CatalogItem { Code = e.Code, Name = e.Name })
                    .ToList()
            });
        }
    }
}

public static class GetBankCatalogQueryEndpoint
{
    public static RouteGroupBuilder GetBankCatalogGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/catalog",
                async ([FromQuery] bool onlyAvailable, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<GetBankCatalog.GetBankCatalogResponse>>(
                        new GetBankCatalog.GetBankCatalogQuery(onlyAvailable));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("GetBankCatalog")
            .MapToApiVersion(1, 0)
            .Produces<GetBankCatalog.GetBankCatalogResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}