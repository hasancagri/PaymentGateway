using Payment.Api.Domains.BinCards.Features.Queries;

namespace Payment.Api.Domains.Payments.Features.Queries;

public static class GetInstallmentOptions
{
    public record GetInstallmentOptionsQuery(string BinNumber, decimal Amount);

    public class GetInstallmentOptionsResponse
    {
        public List<InstallmentOption> Options { get; set; } = new();
    }

    /// <summary>Her taksit sayısı için en düşük maliyetli bankanın teklifi.</summary>
    public class InstallmentOption
    {
        public int InstallmentCount { get; set; }
        public string BankCode { get; set; } = string.Empty;
        public decimal CommissionRatePercent { get; set; }
        public decimal CommissionCost { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal MonthlyAmount { get; set; }
    }

    public class GetInstallmentOptionsQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetInstallmentOptionsResponse>> Handle(
            GetInstallmentOptionsQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var accounts = (await session.Query<PosAccount>()
                .Where(a => a.IsActive && !a.IsDeleted)
                .ToListAsync(ct)).ToList();

            var card = await ResolveBinCard.Resolve(session, query.BinNumber, ct);

            var response = new GetInstallmentOptionsResponse();

            // BIN katalogda yok → taksit türetilecek kart bilgisi yok; boş sonuç (uydurma default üretme).
            if (card is null)
                return FeatureObjectResultModel<GetInstallmentOptionsResponse>.Ok(response);

            var installmentCounts = accounts
                .SelectMany(a => a.CommissionRates.Select(r => r.InstallmentCount))
                .Distinct()
                .OrderBy(i => i);

            foreach (var installmentCount in installmentCounts)
            {
                var route = BankRouter.Route(query.Amount, installmentCount, card, accounts);
                if (!route.IsSuccess)
                    continue;

                var best = route.Data![0];
                var total = query.Amount + best.CommissionCost;

                response.Options.Add(new InstallmentOption
                {
                    InstallmentCount = installmentCount,
                    BankCode = best.Account.BankCode,
                    CommissionRatePercent = best.CommissionRatePercent,
                    CommissionCost = best.CommissionCost,
                    TotalAmount = total,
                    MonthlyAmount = Math.Round(total / installmentCount, 2)
                });
            }

            return FeatureObjectResultModel<GetInstallmentOptionsResponse>.Ok(response);
        }
    }
}

public static class GetInstallmentOptionsQueryEndpoint
{
    public static RouteGroupBuilder GetInstallmentOptionsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/installments",
                async ([FromQuery] string bin, [FromQuery] decimal amount, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<GetInstallmentOptions.GetInstallmentOptionsResponse>>(
                            new GetInstallmentOptions.GetInstallmentOptionsQuery(bin, amount));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("GetInstallmentOptions")
            .MapToApiVersion(1, 0)
            .Produces<GetInstallmentOptions.GetInstallmentOptionsResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}