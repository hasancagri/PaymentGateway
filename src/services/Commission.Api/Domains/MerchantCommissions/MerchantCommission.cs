using Commission.Api.Domains.BankCommissions;

namespace Commission.Api.Domains.MerchantCommissions;

/// <summary>
/// Merchant'ın gateway'e ödediği komisyon (gelir). Belirli bir BankCommission'a bağlıdır;
/// invariant onun oranına karşı: <c>Rate &gt; bankCommission.Rate</c> (kesin büyük, eşit reddedilir).
/// Tenant YOK (düz MerchantId filtresi). MerchantId Merchant.Api'ye çağrı YAPILMADAN Guid olarak tutulur.
/// </summary>
public class MerchantCommission : AggregateRoot
{
    private MerchantCommission()
    {
    }

    public Guid MerchantId { get; private set; }
    public Guid BankCommissionId { get; private set; }

    /// <summary>Bağlı banka oranından snapshot (okuma kolaylığı).</summary>
    public Criteria Criteria { get; private set; } = null!;

    /// <summary>Bağlı banka kodundan snapshot.</summary>
    public string BankCode { get; private set; } = string.Empty;

    /// <summary>Yüzde oran; invariant: banka oranından kesin büyük.</summary>
    public decimal Rate { get; private set; }

    public static ResultDomain<MerchantCommission> Create(Guid merchantId, BankCommission bankCommission, decimal rate)
    {
        if (merchantId == Guid.Empty)
        {
            return ResultDomain<MerchantCommission>.Error(new MessageItem
            {
                Property = nameof(MerchantId),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });
        }

        if (bankCommission is null)
        {
            return ResultDomain<MerchantCommission>.Error(new MessageItem
            {
                Property = nameof(BankCommissionId),
                Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
            });
        }

        if (rate <= bankCommission.Rate)
            return ResultDomain<MerchantCommission>.Error(RateMustExceedBankRate());

        return ResultDomain<MerchantCommission>.Ok(new MerchantCommission
        {
            MerchantId = merchantId,
            BankCommissionId = bankCommission.Id,
            Criteria = bankCommission.Criteria,
            BankCode = bankCommission.BankCode,
            Rate = rate
        });
    }

    public ResultDomain UpdateRate(decimal rate, BankCommission bankCommission)
    {
        if (bankCommission is null)
        {
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(BankCommissionId),
                Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
            });
        }

        if (rate <= bankCommission.Rate)
            return ResultDomain.Error(RateMustExceedBankRate());

        Rate = rate;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    private static MessageItem RateMustExceedBankRate() => new()
    {
        Property = nameof(Rate),
        Code = CommissionResourceConstants.MERCHANT_RATE_MUST_EXCEED_BANK_RATE
    };
}