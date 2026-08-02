namespace Payment.Api.Domains.BinCards.Features.Queries;

/// <summary>
/// Tekil BIN detayı (Admin görüntüleme): ham katalog alanları + türetilmiş taksit-banka listesi.
/// 8→6 fallback; bulunamazsa null. Taksit-banka <see cref="ResolveBinCard.DeriveInstallmentBankCodes"/>
/// ile üretilir (008 paritesi). Enum'lar yanıtta string ad (Admin enum tipine bağımlı olmasın).
/// </summary>
public static class GetBinCardDetail
{
    public record GetBinCardDetailQuery(string Bin);

    public class BinCardDetailResponse
    {
        public string BinNumber { get; set; } = string.Empty;
        public string BankCode { get; set; } = string.Empty;
        public string CardType { get; set; } = string.Empty;
        public string CardBrand { get; set; } = string.Empty;
        public string CardProgram { get; set; } = string.Empty;
        public bool Commercial { get; set; }
        public IReadOnlyList<string> InstallmentBankCodes { get; set; } = Array.Empty<string>();
    }

    public class GetBinCardDetailQueryHandler
    {
        public async Task<FeatureObjectResultModel<BinCardDetailResponse>> Handle(
            GetBinCardDetailQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var response = await ResolveDetailAsync(session, query.Bin, ct);
            return FeatureObjectResultModel<BinCardDetailResponse>.Ok(response); // null → NotFound
        }
    }

    /// <summary>Katalogtan hedef BinCard'ı 8→6 fallback ile bul, detay + taksit-banka türet; yoksa null.</summary>
    public static async Task<BinCardDetailResponse?> ResolveDetailAsync(IQuerySession session, string bin, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bin))
            return null;

        var target = await session.Query<BinCard>().FirstOrDefaultAsync(x => x.BinNumber == bin, ct);
        if (target is null && bin.Length > 6)
        {
            var truncated = bin[..6];
            target = await session.Query<BinCard>().FirstOrDefaultAsync(x => x.BinNumber == truncated, ct);
        }

        if (target is null)
            return null;

        var sameProgramCards = target.CardType == CardType.Credit && target.CardProgram != CardProgram.Unknown
            ? await session.Query<BinCard>().Where(x => x.CardProgram == target.CardProgram).ToListAsync(ct)
            : (IReadOnlyList<BinCard>)Array.Empty<BinCard>();

        return new BinCardDetailResponse
        {
            BinNumber = target.BinNumber,
            BankCode = target.BankCode,
            CardType = target.CardType.ToString(),
            CardBrand = target.CardBrand.ToString(),
            CardProgram = target.CardProgram.ToString(),
            Commercial = target.Commercial,
            InstallmentBankCodes = ResolveBinCard.DeriveInstallmentBankCodes(target, sameProgramCards)
        };
    }
}
