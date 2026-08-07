namespace Commission.Api.Domains.MerchantCommissions.Features.Commands;

/// <summary>
/// Tek merchant komisyonu oluştur/güncelle (upsert). Banka-bağımsız: banka YÜKLENMEZ, banka bağı YOK.
/// <c>(MerchantId, Criteria)</c> varsa oran güncellenir; yoksa yeni kayıt.
/// </summary>
public static class CreateMerchantCommission
{
    public record CriteriaDto(
        string CardBrand,
        string CardType,
        string TransactionRegion,
        int InstallmentCount);

    public record CreateMerchantCommissionCommand(
        Guid MerchantId,
        CriteriaDto Criteria,
        decimal Rate);

    public class CreateMerchantCommissionResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateMerchantCommissionCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateMerchantCommissionResponse>> Handle(
            CreateMerchantCommissionCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            // Kriteri kod stringlerinden kur (enum parse + taksit doğrulama). Banka yüklenmez.
            var criteriaResult = Criteria.FromCodes(cmd.Criteria.CardBrand, cmd.Criteria.CardType,
                cmd.Criteria.TransactionRegion, cmd.Criteria.InstallmentCount);
            if (!criteriaResult.IsSuccess)
                return FeatureObjectResultModel<CreateMerchantCommissionResponse>.Error(criteriaResult.Messages);

            var criteria = criteriaResult.Data!;

            // (MerchantId, Criteria) mevcutsa upsert. Nested VO LINQ'tan kaçın: belleğe alıp eşitlikle eşleştir.
            var existingForMerchant = await session.Query<MerchantCommission>()
                .Where(c => c.MerchantId == cmd.MerchantId && !c.IsDeleted)
                .ToListAsync(ct);

            var existing = existingForMerchant.FirstOrDefault(c => Equals(c.Criteria, criteria));

            if (existing is not null)
            {
                var updateResult = existing.UpdateRate(cmd.Rate);
                if (!updateResult.IsSuccess)
                    return FeatureObjectResultModel<CreateMerchantCommissionResponse>.Error(updateResult.Messages);

                session.Store(existing);
                return FeatureObjectResultModel<CreateMerchantCommissionResponse>.Ok(new CreateMerchantCommissionResponse
                {
                    Id = existing.Id
                });
            }

            var result = MerchantCommission.Create(cmd.MerchantId, criteria, cmd.Rate);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<CreateMerchantCommissionResponse>.Error(result.Messages);

            session.Store(result.Data!);

            return FeatureObjectResultModel<CreateMerchantCommissionResponse>.Ok(new CreateMerchantCommissionResponse
            {
                Id = result.Data!.Id
            });
        }
    }
}

public static class CreateMerchantCommissionCommandEndpoint
{
    public static RouteGroupBuilder CreateMerchantCommissionGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/",
                async ([FromBody] CreateMerchantCommission.CreateMerchantCommissionCommand cmd, IMessageBus bus) =>
                {
                    var result =
                        await bus.InvokeAsync<FeatureObjectResultModel<CreateMerchantCommission.CreateMerchantCommissionResponse>>(cmd);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("CreateMerchantCommission")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.CommissionWrite)
            .Produces<CreateMerchantCommission.CreateMerchantCommissionResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}