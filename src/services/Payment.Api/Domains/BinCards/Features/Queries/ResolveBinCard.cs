namespace Payment.Api.Domains.BinCards.Features.Queries;

/// <summary>
/// BIN → <see cref="CardInfo"/> katalogdan çözer. Bulunamazsa <c>null</c> (istisna yok, sahte-default
/// yok). 8 hane girdi: tam eşleşme yoksa ilk 6 ile çözülür. Taksit-banka listesi çözüm anında
/// türetilir (CP.VPOS paritesi). Ödeme/taksit okuma yolunun tükettiği çözüm.
/// </summary>
public static class ResolveBinCard
{
    public record ResolveBinCardQuery(string BinNumber);

    public class ResolveBinCardQueryHandler
    {
        public Task<CardInfo?> Handle(ResolveBinCardQuery query, IQuerySession session, CancellationToken ct)
            => Resolve(session, query.BinNumber, ct);
    }

    /// <summary>Katalogdan (DB) çözer: exact-match, sonra 8→6 truncate fallback; yoksa null.</summary>
    public static async Task<CardInfo?> Resolve(IQuerySession session, string binNumber, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(binNumber))
            return null;

        var target = await session.Query<BinCard>().FirstOrDefaultAsync(x => x.BinNumber == binNumber, ct);
        if (target is null && binNumber.Length > 6)
        {
            var truncated = binNumber[..6]; // ifade ağacı range içeremez → önce yerelde hesapla
            target = await session.Query<BinCard>().FirstOrDefaultAsync(x => x.BinNumber == truncated, ct);
        }

        if (target is null)
            return null;

        // Taksit-banka türetme yalnız kredi + geçerli program için gerekir; aksi halde DB'ye gitme.
        var sameProgramCards = NeedsInstallmentDerivation(target)
            ? await session.Query<BinCard>().Where(x => x.CardProgram == target.CardProgram).ToListAsync(ct)
            : (IReadOnlyList<BinCard>)Array.Empty<BinCard>();

        return ToCardInfo(target, sameProgramCards);
    }

    /// <summary>Saf çözüm — in-memory katalog üzerinden (birim test). DB yolu ile aynı kurallar.</summary>
    public static CardInfo? Resolve(string binNumber, IReadOnlyList<BinCard> catalog)
    {
        var target = SelectTarget(binNumber, catalog);
        if (target is null)
            return null;

        var sameProgramCards = catalog.Where(c => c.CardProgram == target.CardProgram).ToList();
        return ToCardInfo(target, sameProgramCards);
    }

    /// <summary>Saf: exact-match, sonra 8→6 truncate fallback; yoksa null.</summary>
    public static BinCard? SelectTarget(string? binNumber, IReadOnlyList<BinCard> catalog)
    {
        if (string.IsNullOrWhiteSpace(binNumber))
            return null;

        var hit = catalog.FirstOrDefault(c => c.BinNumber == binNumber);
        if (hit is null && binNumber.Length > 6)
            hit = catalog.FirstOrDefault(c => c.BinNumber == binNumber[..6]);

        return hit;
    }

    /// <summary>Saf: hedef kart + aynı program kayıtları → <see cref="CardInfo"/>.</summary>
    public static CardInfo ToCardInfo(BinCard target, IReadOnlyList<BinCard> sameProgramCards) =>
        new(target.BankCode, target.CardType == CardType.Credit, DeriveInstallmentBankCodes(target, sameProgramCards));

    /// <summary>
    /// Saf, CP.VPOS paritesi: kredi + geçerli program ise aynı programı destekleyen bankalar
    /// (destek-azalan sıralı, distinct), kartın bankası listede yoksa eklenip başa alınır. Aksi
    /// halde boş liste (banka kartı / bilinmeyen program taksit yapamaz).
    /// </summary>
    public static IReadOnlyList<string> DeriveInstallmentBankCodes(BinCard target, IReadOnlyList<BinCard> sameProgramCards)
    {
        if (!NeedsInstallmentDerivation(target))
            return Array.Empty<string>();

        var banks = sameProgramCards
            .Where(c => c.CardProgram == target.CardProgram)
            .GroupBy(c => c.BankCode)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .ToList();

        if (!banks.Contains(target.BankCode))
            banks.Add(target.BankCode);

        if (banks.IndexOf(target.BankCode) != 0)
        {
            banks.Remove(target.BankCode);
            banks.Insert(0, target.BankCode);
        }

        return banks;
    }

    private static bool NeedsInstallmentDerivation(BinCard card) =>
        card.CardType == CardType.Credit && card.CardProgram != CardProgram.Unknown;
}