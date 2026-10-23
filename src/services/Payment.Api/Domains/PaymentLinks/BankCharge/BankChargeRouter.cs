using Payment.Api.Domains.BinCards;

namespace Payment.Api.Domains.PaymentLinks.BankCharge;

/// <summary>
/// 090/092/093 (İlke VII): banka çekim yönlendirici domain-service'i. "Öde" senkron kullanıcı-tetikli →
/// Process/Saga DEĞİL. Aday seçimi (saf hesap): BIN'den kart ailesi (issuer/şema/tip) çöz, yetenek ele
/// (kart tipi/işlem), her aday için **efektif maliyet = öngörü oranı(şema,tip,taksit) × tutar** hesapla,
/// **en düşük maliyetten** sırala (on-us yalnız eşit maliyette tiebreaker — 092). En avantajlı banka TAKSİT
/// SAYISINA göre değişir (093). <b>Failover YOK (093 kararı):</b> yalnız o taksitin en-avantajlı (gösterilen)
/// acquirer'ı denenir — reddederse işlem başarısız (gösterilen banka = çekilen banka; müşteri tekrar dener).
/// Oran matrisi `BankCommissionRate` read-model'inden (banka otorite, PG öngörü). In-doubt kurtarma (AYNI
/// OrderId durum-sorgu) adapter/Process'te korunur — o failover değil, aynı bankanın çekimini doğrulamadır.
/// </summary>
public sealed class BankChargeRouter
{
    private readonly IReadOnlyList<IBankPaymentAdapter> _adapters;
    private readonly PosAccountOptions _posAccounts;
    private readonly BankRoutingOptions _routing;

    public BankChargeRouter(IEnumerable<IBankPaymentAdapter> adapters, PosAccountOptions posAccounts,
        BankRoutingOptions routing)
    {
        _adapters = adapters.ToList();
        _posAccounts = posAccounts;
        _routing = routing;
    }

    /// <summary>Seçilen taksit için en-avantajlı (gösterilen) acquirer'ı dener. Dönen liste 0 (aday yok →
    /// handler PROVIDER_UNAVAILABLE) ya da 1 deneme (gösterilen banka sonucu). FAILOVER YOK — sıradaki bankaya
    /// geçilmez (093: gösterilen = çekilen).</summary>
    public async Task<IReadOnlyList<RoutedAttempt>> RouteAsync(
        BankChargeRequest request, IReadOnlyList<BinCard> binCards,
        IReadOnlyList<BankCommissionRate> rates, CancellationToken ct)
    {
        var candidates = SelectCandidates(request.Card.Pan, binCards, request.Amount, request.InstallmentCount, rates);
        var attempts = new List<RoutedAttempt>();

        // 093: bağlı-acquirer — yalnız o taksitin en avantajlı (müşteriye gösterilen) bankası denenir.
        var bound = candidates.FirstOrDefault();
        if (bound is null) return attempts;

        var adapter = _adapters.FirstOrDefault(a => a.BankCode == bound.BankCode);
        if (adapter is null) return attempts;

        var result = await adapter.ChargeAsync(request, ct);
        if (result.IsSuccess && result.Data is not null)
            attempts.Add(new RoutedAttempt(bound.BankCode, result.Data));

        return attempts; // red (Issuer/Fraud/Technical/Config) → tek deneme döner; handler başarısız verir (failover yok)
    }

    /// <summary>3DS banka seçimi — PAN YOK (kart tarayıcıdan bankaya, İlke VI) → BIN/aile/oran routing yapılamaz.
    /// Kararlı fallback sırasında ilk 3D-yetenekli canlı adapter. Yoksa null. (Maliyet-sıra seam — taksit/aile
    /// quote'u NonSecure'a özel; 3DS maliyet-önceliği sonraki iş.)</summary>
    public IBankPaymentAdapter? SelectThreeDAdapter()
    {
        foreach (var acc in _posAccounts.Accounts
                     .OrderBy(a => FallbackOrder(a.BankCode))
                     .ThenBy(a => a.BankCode, StringComparer.Ordinal))
        {
            var adapter = _adapters.FirstOrDefault(a => a.BankCode == acc.BankCode);
            if (adapter is IThreeDCapableAdapter) return adapter;
        }

        return null;
    }

    /// <summary>Aday kur (saf hesap, domain-TDD). Yetenek eleme → efektif maliyet = oran(şema,tip,taksit) × tutar
    /// → en düşük maliyetten sırala; on-us yalnız eşit maliyette tiebreaker (092). Bilinmeyen BIN → aile null,
    /// on-us yok, oran=DefaultCommissionRate (fail-open). Oran dokümanı hiç yoksa aday en sona (düşmez). İlk eleman
    /// = o taksitin en avantajlı (gösterilecek + çekilecek) bankası. Quote da bunu kullanır (tutarlılık).</summary>
    public IReadOnlyList<RoutingCandidate> SelectCandidates(
        string pan, IReadOnlyList<BinCard> binCards, decimal amount, int installment,
        IReadOnlyList<BankCommissionRate> rates)
    {
        var issuer = BinCard.ResolveByPan(binCards, pan);
        var scheme = issuer?.Scheme;
        var cardType = issuer?.CardType;

        var candidates = new List<RoutingCandidate>();
        foreach (var acc in _posAccounts.Accounts)
        {
            if (!acc.SupportsNonSecure) continue; // yetenek eleme: işlem desteği
            if (_adapters.All(a => a.BankCode != acc.BankCode)) continue; // canlı adapter yoksa aday olamaz
            if (cardType is not null &&
                !acc.SupportedCardTypes.Contains(cardType, StringComparer.OrdinalIgnoreCase))
                continue; // yetenek eleme: kart tipi

            var isOnUs = issuer is not null &&
                         string.Equals(acc.BankCode, issuer.IssuerBankCode, StringComparison.Ordinal);

            // Öngörü oran (banka otorite, PG kopyası — İlke VII). Oran dokümanı hiç yok → en sona (güvenli, düşmez).
            var rateDoc = rates.FirstOrDefault(r => r.BankCode == acc.BankCode);
            var rate = rateDoc?.RateFor(scheme, cardType, installment);
            var effectiveCost = rate is { } r ? r * amount : decimal.MaxValue;
            candidates.Add(new RoutingCandidate(acc.BankCode, isOnUs, effectiveCost, rate ?? 0m));
        }

        // Sıralama: EFEKTİF MALİYET artan → (eşitlikte) on-us önce → kararlı fallback → banka kodu.
        return candidates
            .OrderBy(c => c.EffectiveCost)
            .ThenByDescending(c => c.IsOnUs)
            .ThenBy(c => FallbackOrder(c.BankCode))
            .ThenBy(c => c.BankCode, StringComparer.Ordinal)
            .ToList();
    }

    private int FallbackOrder(string bankCode)
    {
        var idx = _routing.OrderedBankCodes.IndexOf(bankCode);
        return idx < 0 ? int.MaxValue : idx;
    }
}

// Router aday sıralama çıktısı (saf hesap). EffectiveCost = oran×tutar (sıralama); EffectiveRate = o taksit/aile
// öngörü oranı (quote FinalAmount = tutar×(1+oran) için). on-us yalnız eşit maliyette tiebreaker.
public sealed record RoutingCandidate(string BankCode, bool IsOnUs, decimal EffectiveCost, decimal EffectiveRate);

// Bir bankaya yapılan denemenin sonucu (handler PaymentLink.RecordAttempt'e besler).
public sealed record RoutedAttempt(string BankCode, BankChargeOutcome Outcome);
