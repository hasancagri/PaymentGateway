namespace Payment.Api.Domains.Payments;

/// <summary>
/// Kartın domain temsili. CP.VPOS'un BIN sorgu sonucundan handler'da map'lenir;
/// domain katmanı CP.VPOS tiplerini görmez.
/// </summary>
public record CardInfo(string? BankCode, bool IsCreditCard, IReadOnlyList<string> InstallmentBankCodes);

/// <summary>Yönlendirme adayı: hesaplanan komisyon maliyetiyle birlikte banka POS anlaşması.</summary>
public record RouteCandidate(PosAccount Account, decimal CommissionRatePercent, decimal CommissionCost);

/// <summary>
/// Hesaplamalı banka yönlendirmesi (saf domain service): komisyon oranı, kart BIN'i/programı
/// ve taksit desteğine göre uygun bankaları maliyet sırasıyla döner. Banka çağrısı yapmaz;
/// failover, handler'ın sıralı listeyi denemesiyle gerçekleşir.
/// </summary>
public static class BankRouter
{
    public static ResultDomain<List<RouteCandidate>> Route(
        decimal amount,
        int installmentCount,
        CardInfo card,
        IReadOnlyList<PosAccount> accounts)
    {
        if (amount <= 0 || installmentCount < 1)
        {
            return ResultDomain<List<RouteCandidate>>.Error(new MessageItem
            {
                Property = nameof(amount),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_RANGE
            });
        }

        // Taksit yalnız kredi kartına ve kartın programını destekleyen bankalara yapılabilir.
        if (installmentCount > 1 && !card.IsCreditCard)
        {
            return ResultDomain<List<RouteCandidate>>.Error(new MessageItem
            {
                Property = nameof(card),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR
            });
        }

        var candidates = accounts
            .Where(a => a.IsActive && !a.IsDeleted)
            .Where(a => installmentCount == 1 || card.InstallmentBankCodes.Contains(a.BankCode))
            .Select(a => new { Account = a, Rate = a.GetCommissionRate(installmentCount) })
            .Where(x => x.Rate is not null)
            .Select(x => new RouteCandidate(x.Account, x.Rate!.Value, Math.Round(amount * x.Rate!.Value / 100m, 2)))
            .OrderBy(c => c.CommissionCost)
            // Maliyet eşitse kartın kendi bankası öne geçer (onay oranı en yüksek yol).
            .ThenByDescending(c => c.Account.BankCode == card.BankCode)
            .ToList();

        if (candidates.Count == 0)
        {
            return ResultDomain<List<RouteCandidate>>.Error(new MessageItem
            {
                Property = nameof(accounts),
                Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
            });
        }

        return ResultDomain<List<RouteCandidate>>.Ok(candidates);
    }
}