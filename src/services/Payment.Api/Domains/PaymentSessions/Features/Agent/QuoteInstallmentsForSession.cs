using Payment.Api.CardVault;
using Payment.Api.Domains.PosAccounts;

namespace Payment.Api.Domains.PaymentSessions.Features.Agent;

/// <summary>
/// Faz 1 (quote) — agent'a açık slice. Token'ı vault'ta karta çözer, oturumu açar ve
/// <b>Model A</b> taksit listesini üretir: kullanıcı tutarı = sepet tutarı (komisyon EKLENMEZ,
/// FR-010/011). Komisyon yalnız <see cref="BankRouter"/>'da en ucuz POS'u seçmek için girer.
/// Desteklenmeyen taksit listede görünmez (FR-008); banka kartı → yalnız peşin (FR-009).
/// Boş liste → oturum Failed. (Mevcut Model B <c>GetInstallmentOptions</c> bu akışta KULLANILMAZ.)
/// </summary>
public static class QuoteInstallmentsForSession
{
    public record QuoteInstallmentsForSessionCommand(string CardToken, decimal CartAmount);

    public class QuoteInstallmentsForSessionResponse
    {
        public Guid SessionId { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<InstallmentLine> Installments { get; set; } = new();

        public static QuoteInstallmentsForSessionResponse From(PaymentSession session) => new()
        {
            SessionId = session.Id,
            Status = session.Status.ToString(),
            Installments =
            [
                .. session.OfferedInstallments
                    .Select(o => new InstallmentLine
                    {
                        InstallmentCount = o.InstallmentCount,
                        UserTotalAmount = o.UserTotalAmount,
                        MonthlyAmount = o.MonthlyAmount
                    })
            ]
        };
    }

    public class InstallmentLine
    {
        public int InstallmentCount { get; set; }
        public decimal UserTotalAmount { get; set; }
        public decimal MonthlyAmount { get; set; }
    }

    /// <summary>
    /// Saf, DB'siz Model A hesabı (birim test edilir). Taksit sayıları POS komisyon gridinden
    /// türetilir (sabit liste yok, FR-008); her sayı için <see cref="BankRouter"/> destekleyen en
    /// ucuz POS'u bulursa satır eklenir. Satır tutarı = <paramref name="cartAmount"/> (Model A);
    /// aylık = tutar / taksit (2 hane). Banka kartında n&gt;1 BankRouter'da elenir → yalnız peşin.
    /// </summary>
    public static List<OfferedInstallment> BuildOfferedInstallments(
        CardInfo card, decimal cartAmount, IReadOnlyList<PosAccount> accounts)
    {
        var result = new List<OfferedInstallment>();

        var installmentCounts = accounts
            .SelectMany(a => a.CommissionRates.Select(r => r.InstallmentCount))
            .Distinct()
            .OrderBy(i => i);

        foreach (var installmentCount in installmentCounts)
        {
            var route = BankRouter.Route(cartAmount, installmentCount, card, accounts);
            if (!route.IsSuccess)
                continue; // bu taksiti destekleyen aktif POS yok → listeye girmez

            result.Add(new OfferedInstallment(
                installmentCount,
                cartAmount, // Model A: komisyon eklenmez
                Math.Round(cartAmount / installmentCount, 2)));
        }

        return result;
    }

    [Transactional]
    public class QuoteInstallmentsForSessionCommandHandler
    {
        public async Task<FeatureObjectResultModel<QuoteInstallmentsForSessionResponse>> Handle(
            QuoteInstallmentsForSessionCommand command,
            IDocumentSession session,
            ICardVault vault,
            CancellationToken ct)
        {
            // Token → kart (server-side, PAN yok). Geçersiz token → hata, kart verisi sızmadan (FR-019).
            var cardResult = await vault.ResolveCardInfoAsync(command.CardToken, ct);
            if (!cardResult.IsSuccess)
                return FeatureObjectResultModel<QuoteInstallmentsForSessionResponse>.Error(cardResult.Messages);

            var createResult = PaymentSession.Create(command.CardToken, command.CartAmount);
            if (!createResult.IsSuccess)
                return FeatureObjectResultModel<QuoteInstallmentsForSessionResponse>.Error(createResult.Messages);

            var paymentSession = createResult.Data!;

            var accounts = (await session.Query<PosAccount>()
                .Where(a => a.IsActive && !a.IsDeleted)
                .ToListAsync(ct)).ToList();

            var offered = BuildOfferedInstallments(cardResult.Data!, command.CartAmount, accounts);

            // Boş liste → OfferInstallments oturumu Failed'e alır (Ok döner; response Status=Failed taşır).
            var offerResult = paymentSession.OfferInstallments(offered);
            if (!offerResult.IsSuccess)
                return FeatureObjectResultModel<QuoteInstallmentsForSessionResponse>.Error(offerResult.Messages);

            session.Store(paymentSession);

            return FeatureObjectResultModel<QuoteInstallmentsForSessionResponse>.Ok(
                QuoteInstallmentsForSessionResponse.From(paymentSession));
        }
    }
}