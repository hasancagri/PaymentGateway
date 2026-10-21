using Payment.Api.Domains.BinCards;

namespace Payment.Api.Domains.PaymentLinks.BankCharge;

/// <summary>
/// 090 (FR-005/FR-006/FR-016, İlke VII): banka çekim yönlendirici domain-service'i. "Öde" senkron kullanıcı-
/// tetikli → Process/Saga DEĞİL. İki aşama: (1) aday kur — BIN'den issuer çöz, yetenek ele (kart tipi/işlem),
/// sırala: on-us (issuer == acquirer) önce, sonra CostPriority (bilinmeyen BIN → on-us atla, fail-open);
/// (2) sırayla dene — Approved/IssuerDecline/FraudDecline → DUR, TechnicalFailure/ConfigError → sıradaki aday.
/// Her denenen banka bir sonuç kaydı döndürür (handler PaymentLink.RecordAttempt'e besler).
/// <para><b>Çift-çekim seam (belgeli, ARTIK CANLI):</b> 3 banka canlı → TechnicalFailure'da failover FİİLEN
/// koşar; 1. banka çekmiş olabilir, durum-sorgu YOK → çift-çekim penceresi açık. Simülatörde bakiye YOK →
/// şu an zararsız. GERÇEK banka öncesi durum-sorgu ZORUNLU (sonraki versiyon). Health/circuit =
/// BankRoutingOptions seam (TODO).</para>
/// </summary>
public sealed class BankChargeRouter
{
    private readonly IReadOnlyList<IBankPaymentAdapter> _adapters;
    private readonly PosAccountOptions _posAccounts;
    private readonly BankRoutingOptions _routing;

    public BankChargeRouter(IEnumerable<IBankPaymentAdapter> adapters, PosAccountOptions posAccounts, BankRoutingOptions routing)
    {
        _adapters = adapters.ToList();
        _posAccounts = posAccounts;
        _routing = routing;
    }

    /// <summary>Aday listesini sırayla dener (failover). Dönen liste: denenen her bankanın sonucu (son eleman =
    /// karar veren). Aday yoksa boş liste (handler PROVIDER_UNAVAILABLE'a çevirir).</summary>
    public async Task<IReadOnlyList<RoutedAttempt>> RouteAsync(
        BankChargeRequest request, IReadOnlyList<BinCard> binCards, CancellationToken ct)
    {
        var candidates = SelectCandidates(request.Card.Pan, binCards);
        var attempts = new List<RoutedAttempt>();

        foreach (var candidate in candidates)
        {
            var adapter = _adapters.FirstOrDefault(a => a.BankCode == candidate.BankCode);
            if (adapter is null) continue;

            var result = await adapter.ChargeAsync(request, ct);
            if (!result.IsSuccess || result.Data is null) continue; // yönlendirilemez hata → sıradaki aday

            var outcome = result.Data;
            attempts.Add(new RoutedAttempt(candidate.BankCode, outcome));

            if (outcome.IsApproved) break;                                    // onay → dur
            if (outcome.DeclineClass is DeclineClass.IssuerDecline            // aynı issuer boşuna
                or DeclineClass.FraudDecline) break;                          // fraud asla retry
            // TechnicalFailure / ConfigError → sıradaki adaya geç (failover)
        }

        return attempts;
    }

    /// <summary>3DS banka seçimi — PAN YOK (kart tarayıcıdan bankaya, İlke VI) → BIN/on-us routing yapılamaz.
    /// Maliyet sırasında ilk 3D-yetenekli canlı adapter (on-us optimizasyonu NonSecure'a özel). Yoksa null.</summary>
    public IBankPaymentAdapter? SelectThreeDAdapter()
    {
        foreach (var acc in _posAccounts.Accounts
                     .OrderBy(a => a.CostPriority).ThenBy(a => FallbackOrder(a.BankCode)).ThenBy(a => a.BankCode, StringComparer.Ordinal))
        {
            var adapter = _adapters.FirstOrDefault(a => a.BankCode == acc.BankCode);
            if (adapter is IThreeDCapableAdapter) return adapter;
        }
        return null;
    }

    /// <summary>Aşama 1 — aday kur (saf hesap, domain-TDD). Yetenek eleme → on-us + maliyet sıralaması.</summary>
    public IReadOnlyList<RoutingCandidate> SelectCandidates(string pan, IReadOnlyList<BinCard> binCards)
    {
        var issuer = BinCard.ResolveByPan(binCards, pan);
        var cardType = issuer?.CardType;

        var candidates = new List<RoutingCandidate>();
        foreach (var acc in _posAccounts.Accounts)
        {
            if (!acc.SupportsNonSecure) continue;                              // yetenek eleme: işlem desteği
            if (_adapters.All(a => a.BankCode != acc.BankCode)) continue;      // canlı adapter yoksa aday olamaz
            if (cardType is not null &&
                !acc.SupportedCardTypes.Contains(cardType, StringComparer.OrdinalIgnoreCase))
                continue;                                                      // yetenek eleme: kart tipi

            var isOnUs = issuer is not null &&
                         string.Equals(acc.BankCode, issuer.IssuerBankCode, StringComparison.Ordinal);
            candidates.Add(new RoutingCandidate(acc.BankCode, isOnUs, acc.CostPriority));
        }

        // Sıralama: on-us önce → CostPriority artan → kararlı fallback (OrderedBankCodes indeksi) → banka kodu.
        return candidates
            .OrderByDescending(c => c.IsOnUs)
            .ThenBy(c => c.CostPriority)
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

// Router aday sıralama çıktısı (saf hesap). on-us önce, sonra maliyet.
public sealed record RoutingCandidate(string BankCode, bool IsOnUs, int CostPriority);

// Bir bankaya yapılan denemenin sonucu (handler PaymentLink.RecordAttempt'e besler).
public sealed record RoutedAttempt(string BankCode, BankChargeOutcome Outcome);
