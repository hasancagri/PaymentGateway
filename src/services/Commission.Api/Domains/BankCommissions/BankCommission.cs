
namespace Commission.Api.Domains.BankCommissions;

/// <summary>
/// Gateway'in bankaya ödediği komisyon (maliyet). Global (tenant yok). MerchantCommission
/// invariant'ının referans oranıdır. Benzersizlik: (BankCode, Criteria) — handler kontrol eder.
/// </summary>
public class BankCommission : AggregateRoot
{
    private BankCommission()
    {
    }

    /// <summary>Banka kodu (4 hane; CP.VPOS/PosAccount BankService ile tutarlı).</summary>
    public string BankCode { get; private set; } = string.Empty;

    public Criteria Criteria { get; private set; } = null!;

    /// <summary>Yüzde oran (örn. 1.75); >= 0.</summary>
    public decimal Rate { get; private set; }

    /// <summary>BankCommission oluşturur; bankCode (4 hane), criteria zorunlu, rate >= 0 doğrular.</summary>
    /// <remarks>Handler: CreateBankCommissionCommandHandler, BulkUpsertBankCommissionsCommandHandler</remarks>
    public static ResultDomain<BankCommission> Create(string bankCode, Criteria criteria, decimal rate)
    {
        if (string.IsNullOrWhiteSpace(bankCode) || bankCode.Length != 4)
        {
            return ResultDomain<BankCommission>.Error(new MessageItem
            {
                Property = nameof(BankCode),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT
            });
        }

        if (criteria is null)
        {
            return ResultDomain<BankCommission>.Error(new MessageItem
            {
                Property = nameof(Criteria),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });
        }

        if (rate < 0)
        {
            return ResultDomain<BankCommission>.Error(new MessageItem
            {
                Property = nameof(Rate),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_RANGE
            });
        }

        return ResultDomain<BankCommission>.Ok(new BankCommission
        {
            BankCode = bankCode,
            Criteria = criteria,
            Rate = rate
        });
    }

    /// <summary>Oranı günceller; rate >= 0 doğrular, UpdatedTime'ı yeniler.</summary>
    /// <remarks>Handler: BulkUpsertBankCommissionsCommandHandler</remarks>
    public ResultDomain UpdateRate(decimal rate)
    {
        if (rate < 0)
        {
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(Rate),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_RANGE
            });
        }

        Rate = rate;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }
}