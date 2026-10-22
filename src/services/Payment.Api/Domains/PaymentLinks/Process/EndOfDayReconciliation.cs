namespace Payment.Api.Domains.PaymentLinks.Process;

/// <summary>
/// Phase-1 gün-sonu mutabakat (İlke VII; Ders 16 pull modeli): PG'nin KENDİ dayanıklı süreci — kullanıcı
/// tetikli değil → Domains/ dışı Process/. Her banka için operasyon-API'den kapalı batch özetini çeker
/// `(BankOperationsClient)` ve PG'nin Paid link görünümüyle (banka başına adet + brüt tutar) karşılaştırır.
/// Uyuşmazlık = log uyarısı (gözden geçir). Tetik = günlük cron (internal uç mesajı enqueue eder); mesaj
/// Wolverine outbox'ında dayanıklı (çağrı başarısızsa retry). İşlem-bazlı (BatchDetail OrderId) eşleme = seam.
/// </summary>
public static class EndOfDayReconciliation
{
    public record Run(DateTime AsOf);

    public class RunHandler
    {
        public async Task Handle(Run msg, IDocumentSession session, BankOperationsClient ops,
            InterVposBankOptions banks, ILogger<RunHandler> logger, CancellationToken ct)
        {
            // PG görünümü: Paid link'ler → onaylı deneme bankası başına adet + brüt tutar (bellek-içi grup).
            var paid = await session.Query<PaymentLink>().Where(l => l.Status == PaymentLinkStatus.Paid).ToListAsync(ct);
            var pgByBank = paid
                .GroupBy(l => l.Attempts.FirstOrDefault(a => a.IsApproved)?.BankCode ?? "UNKNOWN")
                .ToDictionary(g => g.Key, g => (Count: g.Count(), Total: g.Sum(l => l.Amount)));

            foreach (var bank in banks.Banks.Where(b => !string.IsNullOrEmpty(b.SubscriptionKey)))
            {
                var batches = await ops.GetClosedBatchesAsync(bank.BankCode, ct);
                pgByBank.TryGetValue(bank.BankCode, out var pg);
                var line = Compare(bank.BankCode, batches, pg.Count, pg.Total);

                if (line.Matched)
                    logger.LogInformation("Mutabakat {Bank}: UYUMLU — adet {Count}, brüt {Gross}", line.BankCode, line.BankCount, line.BankGross);
                else
                    logger.LogWarning("Mutabakat {Bank}: UYUŞMAZLIK — banka(adet {BCount}, brüt {BGross}) ≠ PG(adet {PCount}, brüt {PGross}); gözden geçir",
                        line.BankCode, line.BankCount, line.BankGross, line.PgCount, line.PgTotal);
            }
        }
    }

    /// <summary>Saf karşılaştırma (test-first): banka kapalı-batch toplamı vs PG Paid toplamı. Açık batch
    /// (gün-sonu görmemiş) banka tarafında raporlanmaz → aynı-gün uyuşmazlığı beklenir (gözden-geçir sinyali).</summary>
    public static ReconciliationLine Compare(string bankCode, IReadOnlyList<ClosedBatch> bankBatches, int pgCount, decimal pgTotal)
    {
        var bankCount = bankBatches.Sum(b => b.TransactionCount);
        var bankGross = bankBatches.Sum(b => b.GrossTotal);
        return new ReconciliationLine(bankCode, bankCount, bankGross, pgCount, pgTotal,
            Matched: bankCount == pgCount && bankGross == pgTotal);
    }
}

/// <summary>Banka-başına mutabakat satırı (banka kapalı-batch toplamı vs PG Paid toplamı).</summary>
public sealed record ReconciliationLine(
    string BankCode, int BankCount, decimal BankGross, int PgCount, decimal PgTotal, bool Matched);
