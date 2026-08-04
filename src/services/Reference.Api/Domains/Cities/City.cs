namespace Reference.Api.Domains.Cities;

/// <summary>
/// Şehir referans/katalog kaydı. İş anahtarı: Code. Bir Country'ye işaret eder (çapraz tutarlılık;
/// tüketici tarafında <c>BelongsTo</c> bunun üstünde çalışır). Statik <see cref="Create"/> + invariant.
/// </summary>
public class City : AggregateRoot
{
    private City()
    {
    }

    /// <summary>Şehir kodu. İş anahtarı, immutable.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Şehir adı. Boş değil.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Ait olduğu ülke kodu (normalize upper). Boş değil.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    public static ResultDomain<City> Create(string code, string name, string countryCode)
    {
        if (string.IsNullOrWhiteSpace(code))
            return ResultDomain<City>.Error(Message(nameof(Code), CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT));

        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain<City>.Error(Message(nameof(Name), CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED));

        if (string.IsNullOrWhiteSpace(countryCode))
            return ResultDomain<City>.Error(Message(nameof(CountryCode), CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED));

        return ResultDomain<City>.Ok(new City
        {
            Code = code.Trim(),
            Name = name.Trim(),
            CountryCode = countryCode.Trim().ToUpperInvariant()
        });
    }

    private static MessageItem Message(string property, string code) =>
        new() { Property = property, Code = code };
}