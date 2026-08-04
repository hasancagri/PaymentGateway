namespace Reference.Api.Domains.Countries;

/// <summary>
/// Ülke referans/katalog kaydı (kaynak-of-truth). İş anahtarı: Code (ISO-benzeri, normalize upper).
/// Zengin aggregate: statik <see cref="Create"/> fabrikası + invariant (kod formatı, ad boş değil).
/// v1 salt-okuma (dış yazma yok) ama seed/ileri yönetim için invariant tanımlı.
/// </summary>
public class Country : AggregateRoot
{
    private Country()
    {
    }

    /// <summary>Ülke kodu (normalize upper). İş anahtarı, immutable.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Ülke adı. Boş değil.</summary>
    public string Name { get; private set; } = string.Empty;

    public static ResultDomain<Country> Create(string code, string name)
    {
        var normalized = Normalize(code);
        if (string.IsNullOrWhiteSpace(normalized))
            return ResultDomain<Country>.Error(Message(nameof(Code), CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT));

        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain<Country>.Error(Message(nameof(Name), CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED));

        return ResultDomain<Country>.Ok(new Country { Code = normalized, Name = name.Trim() });
    }

    private static string Normalize(string? code) => code?.Trim().ToUpperInvariant() ?? string.Empty;

    private static MessageItem Message(string property, string code) =>
        new() { Property = property, Code = code };
}