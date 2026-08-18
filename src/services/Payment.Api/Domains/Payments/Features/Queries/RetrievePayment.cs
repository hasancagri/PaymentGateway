namespace Payment.Api.Domains.Payments.Features.Queries;

/// <summary>
/// 039 US2: ödeme retrieve yüzeyi (ECom Order.Api verify + kayıp-yanıt reconcile). `correlationKey`
/// VEYA `paymentId` ile okunur; kiracı-sınırlı (yalnız route {merchantId}'nin ödemesi — FR-010).
/// Bilinmeyen anahtar → 404 (endpoint NotFound; ECom bunu belirsiz sayıp reconcile eder — FR-007).
/// Yanıt charge ile aynı alanlar; Status LOWERCASE (success/failed/pending). Payment write-only'di.
/// </summary>
public static class RetrievePayment
{
    public record RetrieveByKeyQuery(Guid MerchantId, string CorrelationKey);

    public record RetrieveByIdQuery(Guid MerchantId, Guid PaymentId);

    public class RetrievePaymentResponse
    {
        public Guid PaymentId { get; set; }
        public string Status { get; set; } = string.Empty; // success / failed / pending
        public decimal Price { get; set; }
        public decimal PaidPrice { get; set; }
        public string CorrelationKey { get; set; } = string.Empty;
    }

    public class RetrieveByKeyQueryHandler
    {
        public async Task<FeatureObjectResultModel<RetrievePaymentResponse>> Handle(
            RetrieveByKeyQuery query, IQuerySession session, CancellationToken ct)
        {
            var payment = await session.Query<Payment>()
                .Where(x => x.MerchantId == query.MerchantId && x.CorrelationKey == query.CorrelationKey)
                .FirstOrDefaultAsync(ct);
            // Ok(null) → FeatureObjectResultModel otomatik NotFound (endpoint 404'e çevirir).
            return FeatureObjectResultModel<RetrievePaymentResponse>.Ok(payment is null ? null! : MapView(payment));
        }
    }

    public class RetrieveByIdQueryHandler
    {
        public async Task<FeatureObjectResultModel<RetrievePaymentResponse>> Handle(
            RetrieveByIdQuery query, IQuerySession session, CancellationToken ct)
        {
            var payment = await session.LoadAsync<Payment>(query.PaymentId, ct);
            // Kiracı sınırı (FR-010): başka merchant'ın kaydı → yokmuş gibi (404).
            if (payment is null || payment.MerchantId != query.MerchantId)
                return FeatureObjectResultModel<RetrievePaymentResponse>.Ok(null!);
            return FeatureObjectResultModel<RetrievePaymentResponse>.Ok(MapView(payment));
        }
    }

    // Domain durumu → wire yanıt. Status LOWERCASE (ECom RetrieveResult.Map success/failed/pending bekler).
    private static RetrievePaymentResponse MapView(Payment p) => new()
    {
        PaymentId = p.Id,
        Status = p.Status switch
        {
            PaymentStatus.Success => "success",
            PaymentStatus.Failed => "failed",
            _ => "pending" // Charging → belirsiz
        },
        Price = p.Price,
        PaidPrice = p.PaidPrice,
        CorrelationKey = p.CorrelationKey ?? string.Empty
    };
}

public static class RetrievePaymentEndpoint
{
    public static RouteGroupBuilder RetrievePaymentGroupItemEndpoint(this RouteGroupBuilder group)
    {
        // by-key: GET /merchants/{merchantId}/payments?correlationKey=...
        group.MapGet("/",
                async (Guid merchantId, [FromQuery] string? correlationKey, IMessageBus bus) =>
                {
                    if (string.IsNullOrWhiteSpace(correlationKey))
                        return Results.NotFound(); // anahtar yok → bulunamadı (FR-007)
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<RetrievePayment.RetrievePaymentResponse>>(
                        new RetrievePayment.RetrieveByKeyQuery(merchantId, correlationKey));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound();
                })
            .WithName("RetrievePaymentByKey")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationPolicies.MerchantApiKey)
            .Produces<RetrievePayment.RetrievePaymentResponse>()
            .Produces(StatusCodes.Status404NotFound);

        // by-id: GET /merchants/{merchantId}/payments/{paymentId}
        group.MapGet("/{paymentId:guid}",
                async (Guid merchantId, Guid paymentId, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<RetrievePayment.RetrievePaymentResponse>>(
                        new RetrievePayment.RetrieveByIdQuery(merchantId, paymentId));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound();
                })
            .WithName("RetrievePaymentById")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationPolicies.MerchantApiKey)
            .Produces<RetrievePayment.RetrievePaymentResponse>()
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }
}
