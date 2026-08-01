using Commission.Api.Domains.Banks;

namespace Commission.Api.Domains.Banks.Features.Queries;

public static class GetBanks
{
    public record GetBanksQuery(bool IncludeInactive);

    public class GetBanksResponse
    {
        public List<BankItem> Items { get; set; } = new();
    }

    public class BankItem
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<int> SupportedInstallments { get; set; } = new();
        public bool IsActive { get; set; }
    }

    public class GetBanksQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetBanksResponse>> Handle(
            GetBanksQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var q = session.Query<Bank>().Where(b => !b.IsDeleted);

            if (!query.IncludeInactive)
                q = q.Where(b => b.IsActive);

            var banks = await q.ToListAsync(ct);

            return FeatureObjectResultModel<GetBanksResponse>.Ok(new GetBanksResponse
            {
                Items = banks
                    .OrderBy(b => b.Code)
                    .Select(b => new BankItem
                    {
                        Id = b.Id,
                        Code = b.Code,
                        Name = b.Name,
                        SupportedInstallments = b.SupportedInstallments.ToList(),
                        IsActive = b.IsActive
                    }).ToList()
            });
        }
    }
}

public static class GetBanksQueryEndpoint
{
    public static RouteGroupBuilder GetBanksGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/",
                async ([FromQuery] bool includeInactive, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<GetBanks.GetBanksResponse>>(
                        new GetBanks.GetBanksQuery(includeInactive));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("GetBanks")
            .MapToApiVersion(1, 0)
            .Produces<GetBanks.GetBanksResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}