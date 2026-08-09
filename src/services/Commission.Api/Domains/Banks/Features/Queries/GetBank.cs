
namespace Commission.Api.Domains.Banks.Features.Queries;

public static class GetBank
{
    public record GetBankQuery(string Code);

    public class GetBankResponse
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<int> SupportedInstallments { get; set; } = new();
        public bool IsActive { get; set; }
    }

    public class GetBankQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetBankResponse>> Handle(
            GetBankQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var bank = await session.Query<Bank>()
                .Where(b => b.Code == query.Code && !b.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (bank is null)
            {
                return FeatureObjectResultModel<GetBankResponse>.Error(new MessageItem
                {
                    Property = nameof(query.Code),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });
            }

            return FeatureObjectResultModel<GetBankResponse>.Ok(new GetBankResponse
            {
                Id = bank.Id,
                Code = bank.Code,
                Name = bank.Name,
                SupportedInstallments = bank.SupportedInstallments.ToList(),
                IsActive = bank.IsActive
            });
        }
    }
}

public static class GetBankQueryEndpoint
{
    public static RouteGroupBuilder GetBankGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{code}",
                async (string code, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<GetBank.GetBankResponse>>(
                        new GetBank.GetBankQuery(code));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("GetBank")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.CommissionRead)
            .Produces<GetBank.GetBankResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}