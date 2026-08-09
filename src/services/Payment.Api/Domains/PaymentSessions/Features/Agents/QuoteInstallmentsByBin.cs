
namespace Payment.Api.Domains.PaymentSessions.Features.Agents;

/// <summary>
/// BIN-bazlı taksit sorgusu — agent'a açık, <b>read-only</b> slice. Token/PAN/CVV YOK; yalnız
/// kartın <b>BIN</b>'i (ilk 6 hane, hassas değil) + sepet tutarı ile <b>Model A</b> taksit listesini
/// üretir (her satır = sepet tutarı, komisyon eklenmez). BIN → <see cref="CardInfo"/> katalogdan
/// çözülür (<see cref="ResolveBinCard"/>); taksit hesabı <see cref="QuoteInstallmentsForSession"/>
/// ile ORTAK pure mantıktır (<c>BuildOfferedInstallments</c>). Oturum AÇMAZ, hiçbir şey yazmaz —
/// e-ticaret quote-only akışı (024) bunu tüketir; token+seçim akışı <c>get_installment_options</c>'ta kalır.
/// </summary>
public static class QuoteInstallmentsByBin
{
    public record QuoteInstallmentsByBinQuery(string Bin, decimal CartAmount);

    public class QuoteInstallmentsByBinResponse
    {
        public string? Bank { get; set; }
        public bool IsCreditCard { get; set; }
        public List<QuoteInstallmentsForSession.InstallmentLine> Installments { get; set; } = new();
    }

    public class QuoteInstallmentsByBinQueryHandler
    {
        public async Task<FeatureObjectResultModel<QuoteInstallmentsByBinResponse>> Handle(
            QuoteInstallmentsByBinQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            // BIN → kart taksonomisi (banka, kredi/banka, taksit destekleyen bankalar). Bulunamazsa
            // NotFound (sahte-default yok; assistant "uygun taksit yok/BIN çözülemedi" der).
            var card = await ResolveBinCard.Resolve(session, query.Bin, ct);
            if (card is null)
                return FeatureObjectResultModel<QuoteInstallmentsByBinResponse>.NotFound();

            var accounts = (await session.Query<PosAccount>()
                .Where(a => a.IsActive && !a.IsDeleted)
                .ToListAsync(ct)).ToList();

            // Session quote ile AYNI pure Model A hesabı — banka kartı → yalnız peşin, desteklenmeyen
            // taksit listede yok, satır tutarı = sepet tutarı.
            var offered = QuoteInstallmentsForSession.BuildOfferedInstallments(card, query.CartAmount, accounts);

            return FeatureObjectResultModel<QuoteInstallmentsByBinResponse>.Ok(new QuoteInstallmentsByBinResponse
            {
                Bank = card.BankCode,
                IsCreditCard = card.IsCreditCard,
                Installments = offered
                    .Select(o => new QuoteInstallmentsForSession.InstallmentLine
                    {
                        InstallmentCount = o.InstallmentCount,
                        UserTotalAmount = o.UserTotalAmount,
                        MonthlyAmount = o.MonthlyAmount
                    })
                    .ToList()
            });
        }
    }
}