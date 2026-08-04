using Commission.Api.Domains.Banks;
using Commission.Api.ReadModels;

namespace Commission.Api.Domains.Banks.Features.Commands;

public static class CreateBank
{
    // Ad katalogdan gelir; komut yalnız katalog seçimini (Code) + taksitleri taşır.
    public record CreateBankCommand(
        string Code,
        List<int> SupportedInstallments);

    public class CreateBankResponse
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    [Transactional]
    public class CreateBankCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateBankResponse>> Handle(
            CreateBankCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            // Kod benzersizliği (iş anahtarı): aynı Code'lu silinmemiş banka varsa reddet.
            var exists = await session.Query<Bank>()
                .Where(b => b.Code == cmd.Code && !b.IsDeleted)
                .AnyAsync(ct);

            if (exists)
            {
                return FeatureObjectResultModel<CreateBankResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Code),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_DUPLICATE
                });
            }

            // Banka adı Reference-beslemeli read-model'den türer (id = Code); yoksa Create reddeder.
            var reference = await session.LoadAsync<ReferenceBank>(cmd.Code?.Trim() ?? string.Empty, ct);

            var result = Bank.Create(cmd.Code, reference?.Name ?? string.Empty,
                cmd.SupportedInstallments ?? new List<int>());
            if (!result.IsSuccess)
                return FeatureObjectResultModel<CreateBankResponse>.Error(result.Messages);

            session.Store(result.Data!);

            return FeatureObjectResultModel<CreateBankResponse>.Ok(new CreateBankResponse
            {
                Id = result.Data!.Id,
                Code = result.Data.Code
            });
        }
    }
}

public static class CreateBankCommandEndpoint
{
    public static RouteGroupBuilder CreateBankGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/",
                async ([FromBody] CreateBank.CreateBankCommand cmd, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateBank.CreateBankResponse>>(cmd);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("CreateBank")
            .MapToApiVersion(1, 0)
            .Produces<CreateBank.CreateBankResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}