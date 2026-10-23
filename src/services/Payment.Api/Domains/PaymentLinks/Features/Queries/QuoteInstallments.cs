using Payment.Api.Domains.BinCards;

namespace Payment.Api.Domains.PaymentLinks.Features.Queries;

/// <summary>
/// 093 US1 (FR-001..FR-005): Müşteri kartının ilk hanelerini (BIN) girince taksit seçeneklerini hesaplar.
/// Kart ailesi (issuer/şema/tip) `BinCard.ResolveByPan(bin)` ile çözülür; yapılandırılmış her taksit sayısı
/// için router en-avantajlı (en düşük efektif komisyonlu) bankayı seçer (on-us eşit maliyette tiebreaker),
/// `FinalAmount = Amount×(1+oran)` + aylık hesaplanır. En avantajlı banka TAKSİTE göre değişebilir (X 2'de,
/// Y 4'te). Banka adı müşteriye GÖSTERİLİR (kullanıcı kararı; sır değil). Salt okuma; sır/kart verisi içermez.
/// Quote ve charge AYNI seçim mantığını (BankChargeRouter.SelectCandidates) kullanır (tutarlılık).
/// </summary>
public static class QuoteInstallments
{
    public record Query(string OpenToken, string Bin);

    public record InstallmentOption(
        int Installment, string AcquirerBankCode, string AcquirerBankName, decimal TotalAmount, decimal MonthlyAmount);

    public class Response
    {
        public List<InstallmentOption> Options { get; set; } = new();
    }

    public class QuoteInstallmentsHandler
    {
        public async Task<FeatureObjectResultModel<Response>> Handle(
            Query query, IDocumentSession session, BankChargeRouter router,
            PaymentLinkOptions options, PosAccountOptions posAccounts, CancellationToken ct)
        {
            var link = await session.Query<PaymentLink>()
                .Where(x => x.OpenToken == query.OpenToken)
                .FirstOrDefaultAsync(ct);
            if (link is null)
                return FeatureObjectResultModel<Response>.Error(new MessageItem { Code = PaymentLinkResourceConstants.LINK_NOT_FOUND });

            // BIN prefix lookup (tam PAN değil — İlke VI). BIN, BinCard.ResolveByPan'a "pan" olarak verilir.
            var bin = query.Bin;
            var binPrefixes = Enumerable.Range(1, Math.Min(bin.Length, BinCard.MaxBinLength))
                .Select(len => bin[..len]).ToArray();
            var binCards = await session.LoadManyAsync<BinCard>(ct, binPrefixes);
            var rates = await session.Query<BankCommissionRate>().ToListAsync(ct);

            var response = new Response();
            foreach (var n in options.InstallmentCounts)
            {
                var candidates = router.SelectCandidates(bin, binCards, link.Amount, n, rates);
                var best = candidates.FirstOrDefault();
                if (best is null) continue; // o taksitte uygun banka yok → seçenek sunulmaz

                var total = Math.Round(link.Amount * (1 + best.EffectiveRate), 2, MidpointRounding.AwayFromZero);
                var monthly = Math.Round(total / n, 2, MidpointRounding.AwayFromZero);
                var name = posAccounts.Accounts.FirstOrDefault(a => a.BankCode == best.BankCode)?.DisplayName;
                if (string.IsNullOrWhiteSpace(name)) name = best.BankCode;

                response.Options.Add(new InstallmentOption(n, best.BankCode, name, total, monthly));
            }

            return FeatureObjectResultModel<Response>.Ok(response);
        }
    }
}
