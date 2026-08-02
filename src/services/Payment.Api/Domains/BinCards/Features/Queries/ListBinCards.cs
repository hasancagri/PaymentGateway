using Marten.Pagination;

namespace Payment.Api.Domains.BinCards.Features.Queries;

/// <summary>
/// Filtreli + sayfalı ham katalog listesi (Admin görüntüleme). Filtreler (bankCode, cardProgram,
/// cardType, cardBrand, commercial) AND ile birleşir; hiçbiri yoksa (sayfalı) tüm katalog. ~9957
/// kayıt asla tek yanıtta değil — <c>pageSize</c> sunucuda üst sınıra kırpılır. Liste satırında
/// taksit-banka türetme YOK.
/// </summary>
public static class ListBinCards
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public record ListBinCardsQuery(
        string? BankCode,
        string? CardProgram,
        string? CardType,
        string? CardBrand,
        bool? Commercial,
        int Page,
        int PageSize);

    public class BinCardListItem
    {
        public string BinNumber { get; set; } = string.Empty;
        public string BankCode { get; set; } = string.Empty;
        public string CardType { get; set; } = string.Empty;
        public string CardBrand { get; set; } = string.Empty;
        public string CardProgram { get; set; } = string.Empty;
        public bool Commercial { get; set; }
    }

    public class BinCardListResponse
    {
        public List<BinCardListItem> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int PageCount { get; set; }
    }

    /// <summary>Saf: ham query girdisini normalize et — enum ad parse, page/pageSize clamp.</summary>
    public record FilterPlan(
        string? BankCode,
        CardType? CardType,
        CardBrand? CardBrand,
        CardProgram? CardProgram,
        bool? Commercial,
        int Page,
        int PageSize);

    public static FilterPlan PlanFilter(ListBinCardsQuery q)
    {
        return new FilterPlan(
            BankCode: string.IsNullOrWhiteSpace(q.BankCode) ? null : q.BankCode.Trim(),
            CardType: ParseEnum<CardType>(q.CardType),
            CardBrand: ParseEnum<CardBrand>(q.CardBrand),
            CardProgram: ParseEnum<CardProgram>(q.CardProgram),
            Commercial: q.Commercial,
            Page: q.Page < 1 ? 1 : q.Page,
            PageSize: ClampPageSize(q.PageSize));
    }

    public static int ClampPageSize(int pageSize) =>
        pageSize < 1 ? DefaultPageSize : (pageSize > MaxPageSize ? MaxPageSize : pageSize);

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct, Enum =>
        // TryParse sayısal string'i de kabul eder (ör. "123"); yalnız tanımlı ad kabul et.
        !string.IsNullOrWhiteSpace(value)
        && Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
        && Enum.IsDefined(parsed)
            ? parsed
            : null;

    public class ListBinCardsQueryHandler
    {
        public async Task<FeatureObjectResultModel<BinCardListResponse>> Handle(
            ListBinCardsQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var plan = PlanFilter(query);

            var q = session.Query<BinCard>().AsQueryable();
            if (plan.BankCode is not null) q = q.Where(x => x.BankCode == plan.BankCode);
            if (plan.CardType is not null) q = q.Where(x => x.CardType == plan.CardType.Value);
            if (plan.CardBrand is not null) q = q.Where(x => x.CardBrand == plan.CardBrand.Value);
            if (plan.CardProgram is not null) q = q.Where(x => x.CardProgram == plan.CardProgram.Value);
            if (plan.Commercial is not null) q = q.Where(x => x.Commercial == plan.Commercial.Value);

            var paged = await q.OrderBy(x => x.BinNumber).ToPagedListAsync(plan.Page, plan.PageSize, ct);

            var response = new BinCardListResponse
            {
                Items = paged.Select(ToListItem).ToList(),
                TotalCount = (int)paged.TotalItemCount,
                Page = (int)paged.PageNumber,
                PageSize = plan.PageSize,
                PageCount = (int)paged.PageCount
            };

            return FeatureObjectResultModel<BinCardListResponse>.Ok(response);
        }

        private static BinCardListItem ToListItem(BinCard x) => new()
        {
            BinNumber = x.BinNumber,
            BankCode = x.BankCode,
            CardType = x.CardType.ToString(),
            CardBrand = x.CardBrand.ToString(),
            CardProgram = x.CardProgram.ToString(),
            Commercial = x.Commercial
        };
    }
}