namespace Payment.Api.Domains.PosAccounts;

/// <summary>
/// Banka POS anlaşması: sanal POS kimlik bilgileri + taksit başına komisyon oranları.
/// BankRouter'ın hesap girdisidir; kimlik bilgileri handler'da CP.VPOS'un
/// VirtualPOSAuth modeline map'lenir.
/// </summary>
public class PosAccount : AggregateRoot
{
    private PosAccount()
    {
    }

    /// <summary>CP.VPOS banka kodu (4 hane; BankService sabitleri).</summary>
    public string BankCode { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;
    public string MerchantId { get; private set; } = string.Empty;
    public string MerchantUser { get; private set; } = string.Empty;
    public string MerchantPassword { get; private set; } = string.Empty;
    public string? MerchantStorekey { get; private set; }
    public bool TestPlatform { get; private set; }

    [JsonProperty("CommissionRates")] private List<CommissionRate> _commissionRates = new();

    [JsonIgnore] public IReadOnlyList<CommissionRate> CommissionRates => _commissionRates.AsReadOnly();

    /// <summary>POS hesabı oluşturur: banka kodu (4 hane) + zorunlu merchant kimlik bilgilerini doğrular.</summary>
    /// <remarks>Handler: CreatePosAccountCommandHandler</remarks>
    public static ResultDomain<PosAccount> Create(
        string bankCode,
        string title,
        string merchantId,
        string merchantUser,
        string merchantPassword,
        string? merchantStorekey,
        bool testPlatform)
    {
        if (string.IsNullOrWhiteSpace(bankCode) || bankCode.Length != 4)
        {
            return ResultDomain<PosAccount>.Error(new MessageItem
            {
                Property = nameof(BankCode),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT
            });
        }

        if (string.IsNullOrWhiteSpace(merchantId) ||
            string.IsNullOrWhiteSpace(merchantUser) ||
            string.IsNullOrWhiteSpace(merchantPassword))
        {
            return ResultDomain<PosAccount>.Error(new MessageItem
            {
                Property = nameof(MerchantId),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });
        }

        return ResultDomain<PosAccount>.Ok(new PosAccount
        {
            BankCode = bankCode,
            Title = string.IsNullOrWhiteSpace(title) ? bankCode : title,
            MerchantId = merchantId,
            MerchantUser = merchantUser,
            MerchantPassword = merchantPassword,
            MerchantStorekey = merchantStorekey,
            TestPlatform = testPlatform
        });
    }

    /// <summary>Merchant kimlik bilgilerini + test bayrağını günceller; zorunlu alanları doğrular.</summary>
    /// <remarks>Handler: UpdatePosAccountCommandHandler</remarks>
    public ResultDomain UpdateCredentials(
        string merchantId,
        string merchantUser,
        string merchantPassword,
        string? merchantStorekey,
        bool testPlatform)
    {
        if (string.IsNullOrWhiteSpace(merchantId) ||
            string.IsNullOrWhiteSpace(merchantUser) ||
            string.IsNullOrWhiteSpace(merchantPassword))
        {
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(MerchantId),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });
        }

        MerchantId = merchantId;
        MerchantUser = merchantUser;
        MerchantPassword = merchantPassword;
        MerchantStorekey = merchantStorekey;
        TestPlatform = testPlatform;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Taksit için komisyon oranını kurar/günceller (upsert). Oran yüzdedir (örn. 2.35).</summary>
    /// <remarks>Handler: UpdatePosAccountCommandHandler, CreatePosAccountCommandHandler</remarks>
    public ResultDomain SetCommissionRate(int installmentCount, decimal ratePercent)
    {
        if (installmentCount < 1 || ratePercent < 0)
        {
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(CommissionRates),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_RANGE
            });
        }

        _commissionRates.RemoveAll(r => r.InstallmentCount == installmentCount);
        _commissionRates.Add(new CommissionRate(installmentCount, ratePercent));
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Verilen taksit sayısına ait komisyon oranını kaldırır.</summary>
    /// <remarks>Handler: UpdatePosAccountCommandHandler</remarks>
    public void RemoveCommissionRate(int installmentCount)
    {
        _commissionRates.RemoveAll(r => r.InstallmentCount == installmentCount);
        UpdatedTime = DateTime.UtcNow;
    }

    /// <summary>Bu taksit sayısı için tanımlı oran; tanımsızsa null (banka bu taksidi desteklemiyor).</summary>
    /// <remarks>Handler: BankRouter</remarks>
    public decimal? GetCommissionRate(int installmentCount)
    {
        return _commissionRates.FirstOrDefault(r => r.InstallmentCount == installmentCount)?.RatePercent;
    }

    /// <summary>Hesabı aktif eder (IsActive = true).</summary>
    /// <remarks>Handler: UpdatePosAccountCommandHandler</remarks>
    public void Activate()
    {
        IsActive = true;
        UpdatedTime = DateTime.UtcNow;
    }

    /// <summary>Hesabı pasif eder (IsActive = false).</summary>
    /// <remarks>Handler: UpdatePosAccountCommandHandler</remarks>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedTime = DateTime.UtcNow;
    }
}

/// <summary>Value object: taksit sayısı + yüzde komisyon oranı.</summary>
public record CommissionRate(int InstallmentCount, decimal RatePercent);