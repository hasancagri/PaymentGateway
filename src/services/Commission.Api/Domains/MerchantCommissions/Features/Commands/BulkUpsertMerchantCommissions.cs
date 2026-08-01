namespace Commission.Api.Domains.MerchantCommissions.Features.Commands;

/// <summary>
/// Grid kaydı: seçilen merchant + doldurulan hücreler tek atomik işlemde upsert edilir.
/// Mevcut (MerchantId, Criteria) → UpdateRate; yok → Create. Tek-tek POST korunur (geriye uyum).
/// Banka-bağımsız: banka/taksit-destek kontrolü YOK (merchant kombinasyon-bazlı).
/// </summary>
public static class BulkUpsertMerchantCommissions
{
    public record CriteriaDto(
        string CardBrand,
        string CardType,
        string TransactionRegion,
        int InstallmentCount);

    public record Item(CriteriaDto Criteria, decimal Rate);

    public record BulkUpsertMerchantCommissionsCommand(Guid MerchantId, List<Item> Items);

    public class BulkUpsertMerchantCommissionsResponse
    {
        public int Created { get; set; }
        public int Updated { get; set; }
    }

    [Transactional]
    public class BulkUpsertMerchantCommissionsCommandHandler
    {
        public async Task<FeatureObjectResultModel<BulkUpsertMerchantCommissionsResponse>> Handle(
            BulkUpsertMerchantCommissionsCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            // Mevcut komisyonlar (aynı merchant) belleğe: nested VO LINQ'tan kaçın, kardinalite düşük.
            var existing = await session.Query<MerchantCommission>()
                .Where(c => c.MerchantId == cmd.MerchantId && !c.IsDeleted)
                .ToListAsync(ct);

            var response = new BulkUpsertMerchantCommissionsResponse();

            // Aynı kombinasyon bulk içinde iki kez gelirse son değer geçerli: kriterle bellekte izle.
            var touched = new Dictionary<Criteria, MerchantCommission>();

            foreach (var item in cmd.Items ?? new List<Item>())
            {
                var criteriaResult = Criteria.FromCodes(item.Criteria.CardBrand, item.Criteria.CardType,
                    item.Criteria.TransactionRegion, item.Criteria.InstallmentCount);
                if (!criteriaResult.IsSuccess)
                    return FeatureObjectResultModel<BulkUpsertMerchantCommissionsResponse>.Error(criteriaResult.Messages);

                var criteria = criteriaResult.Data!;

                // Aynı istekte daha önce işlenen kriter → onu güncelle (kopya oluşturma).
                if (touched.TryGetValue(criteria, out var pending))
                {
                    var reRate = pending.UpdateRate(item.Rate);
                    if (!reRate.IsSuccess)
                        return FeatureObjectResultModel<BulkUpsertMerchantCommissionsResponse>.Error(reRate.Messages);
                    continue;
                }

                var match = existing.FirstOrDefault(c => Equals(c.Criteria, criteria));
                if (match is not null)
                {
                    var upd = match.UpdateRate(item.Rate);
                    if (!upd.IsSuccess)
                        return FeatureObjectResultModel<BulkUpsertMerchantCommissionsResponse>.Error(upd.Messages);

                    session.Update(match);
                    touched[criteria] = match;
                    response.Updated++;
                }
                else
                {
                    var created = MerchantCommission.Create(cmd.MerchantId, criteria, item.Rate);
                    if (!created.IsSuccess)
                        return FeatureObjectResultModel<BulkUpsertMerchantCommissionsResponse>.Error(created.Messages);

                    session.Store(created.Data!);
                    touched[criteria] = created.Data!;
                    response.Created++;
                }
            }

            return FeatureObjectResultModel<BulkUpsertMerchantCommissionsResponse>.Ok(response);
        }
    }
}

public static class BulkUpsertMerchantCommissionsCommandEndpoint
{
    public static RouteGroupBuilder BulkUpsertMerchantCommissionsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/bulk",
                async ([FromBody] BulkUpsertMerchantCommissions.BulkUpsertMerchantCommissionsCommand cmd, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<BulkUpsertMerchantCommissions.BulkUpsertMerchantCommissionsResponse>>(cmd);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("BulkUpsertMerchantCommissions")
            .MapToApiVersion(1, 0)
            .Produces<BulkUpsertMerchantCommissions.BulkUpsertMerchantCommissionsResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}