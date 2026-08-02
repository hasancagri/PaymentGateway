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

        // Filtreli sayfalı liste (Admin): segmentsiz GET → ham katalog, AND filtre + sayfalama.
        group.MapGet("/",
                async (
                    [FromQuery] string? bankCode,
                    [FromQuery] string? cardProgram,
                    [FromQuery] string? cardType,
                    [FromQuery] string? cardBrand,
                    [FromQuery] bool? commercial,
                    [FromQuery] int page,
                    [FromQuery] int pageSize,
                    IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<ListBinCards.BinCardListResponse>>(
                        new ListBinCards.ListBinCardsQuery(bankCode, cardProgram, cardType, cardBrand, commercial, page, pageSize));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("ListBinCards")
            .MapToApiVersion(1, 0)
            .Produces<ListBinCards.BinCardListResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // Tekil BIN detayı (Admin): ham alanlar + türetilmiş taksit-banka (bulunamazsa 404).
        group.MapGet("/{bin}",
                async (string bin, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<GetBinCardDetail.BinCardDetailResponse>>(
                        new GetBinCardDetail.GetBinCardDetailQuery(bin));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
                })
            .WithName("GetBinCardDetail")
            .MapToApiVersion(1, 0)
            .Produces<GetBinCardDetail.BinCardDetailResponse>()
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