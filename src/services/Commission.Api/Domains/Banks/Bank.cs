namespace Commission.Api.Domains.Banks;

/// <summary>
/// Sistemin tanıdığı bankanın referans/katalog kaydı (lookup verisi). Komisyon
/// kombinasyonlarının filtre kümesi ve "eksik" hesabının referansıdır. Dış-sistem
/// entegrasyonu değil (POS credentials/limit burada tutulmaz). İş anahtarı: Code (benzersiz,
/// handler kontrol eder). Seed edilmez — operatör elle girer.
/// </summary>
public class Bank : AggregateRoot
{
    /// <summary>Desteklenen en yüksek taksit sayısı (mevcut ödeme kısıtıyla tutarlı).</summary>
    public const int MaxInstallment = 15;

    private readonly List<int> _supportedInstallments = new();

    private Bank()
    {
    }

    /// <summary>Banka kodu (tam 4 hane). İş anahtarı, immutable. Kanonik katalogda bulunur.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Banka adı. Katalogdan türer (elle girilmez), immutable.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Desteklenen taksit sayıları (boş değil; her biri 1..MaxInstallment; distinct + artan).</summary>
    public IReadOnlyList<int> SupportedInstallments => _supportedInstallments;

    /// <summary>
    /// Katalogdan seçilen bir kodla banka oluşturur. Ad katalogdan türer (parametre değildir);
    /// kod katalogda yoksa reddedilir.
    /// </summary>
    public static ResultDomain<Bank> Create(string code, IEnumerable<int> installments)
    {
        if (!IsValidCode(code))
            return ResultDomain<Bank>.Error(Message(nameof(Code), CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT));

        if (!BankCatalog.TryGetName(code, out var name))
            return ResultDomain<Bank>.Error(Message(nameof(Code), CommissionResourceConstants.BANK_NOT_IN_CATALOG));

        var normalized = NormalizeInstallments(installments, out var installmentError);
        if (installmentError is not null)
            return ResultDomain<Bank>.Error(installmentError);

        var bank = new Bank
        {
            Code = code,
            Name = name
        };
        bank._supportedInstallments.AddRange(normalized);

        return ResultDomain<Bank>.Ok(bank);
    }

    /// <summary>Aktiflik ve taksit listesini günceller. Code ve Name değişmez (katalogdan).</summary>
    public ResultDomain Update(bool isActive, IEnumerable<int> installments)
    {
        var normalized = NormalizeInstallments(installments, out var installmentError);
        if (installmentError is not null)
            return ResultDomain.Error(installmentError);

        IsActive = isActive;
        _supportedInstallments.Clear();
        _supportedInstallments.AddRange(normalized);
        UpdatedTime = DateTime.UtcNow;

        return ResultDomain.Ok();
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedTime = DateTime.UtcNow;
    }

    private static bool IsValidCode(string code) =>
        !string.IsNullOrWhiteSpace(code) && code.Length == 4 && code.All(char.IsDigit);

    /// <summary>Tekilleştir, sırala, aralık doğrula. Hata varsa error out-parametresi dolar.</summary>
    private static List<int> NormalizeInstallments(IEnumerable<int> installments, out MessageItem? error)
    {
        error = null;
        var list = (installments ?? Enumerable.Empty<int>()).Distinct().OrderBy(x => x).ToList();

        if (list.Count == 0)
        {
            error = Message(nameof(SupportedInstallments), CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED);
            return list;
        }

        if (list.Any(x => x < 1 || x > MaxInstallment))
            error = Message(nameof(SupportedInstallments), CommonResourceConstants.COMMON_MESSAGE_INVALID_RANGE);

        return list;
    }

    private static MessageItem Message(string property, string code) =>
        new() { Property = property, Code = code };
}