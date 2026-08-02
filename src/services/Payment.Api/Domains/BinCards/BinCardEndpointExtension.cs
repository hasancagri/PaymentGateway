using Payment.Api.Domains.BinCards.Features.Commands;
using Payment.Api.Domains.BinCards.Features.Queries;

namespace Payment.Api.Domains.BinCards;

public static class BinCardEndpointExtension
{
    public static void AddBinCardGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/bin-cards")
            .WithTags("bin-cards")
            .WithApiVersionSet(apiVersionSet);

        // Debug/iç çözümleme ucu: BIN → CardInfo (bulunamazsa 404).
        group.MapGet("/{bin}",
                async (string bin, IQuerySession session, CancellationToken ct) =>
                {
                    var card = await ResolveBinCard.Resolve(session, bin, ct);
                    return card is null ? Results.NotFound() : Results.Ok(card);
                })
            .WithName("ResolveBinCard")
            .MapToApiVersion(1, 0)
            .Produces<CardInfo>()
            .Produces(StatusCodes.Status404NotFound);

        // Operatör: yayınlanan listeyi idempotent toplu upsert.
        group.MapPost("/import",
                async ([FromBody] ImportBinCards.ImportBinCardsCommand cmd, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<ImportBinCards.ImportBinCardsResponse>>(cmd);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("ImportBinCards")
            .MapToApiVersion(1, 0)
            .Produces<ImportBinCards.ImportBinCardsResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);
    }
}