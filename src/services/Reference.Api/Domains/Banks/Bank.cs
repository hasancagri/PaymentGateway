namespace Reference.Api.Domains.Banks;

/// <summary>
/// Banka referans/katalog kaydı — yalnız code→ad. Komisyon-özel öznitelik (taksit/aktiflik) BURADA
/// DEĞİL (Commission BC'de kalır). İş anahtarı: Code (tam 4 hane). Statik <see cref="Create"/> + invariant.
/// </summary>
public class Bank : AggregateRoot
{
    private Bank()
    {
    }

    /// <summary>Banka kodu (tam 4 hane). İş anahtarı, immutable.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Banka adı. Boş değil.</summary>
    public string Name { get; private set; } = string.Empty;

    public static ResultDomain<Bank> Create(string code, string name)
    {
        if (!IsValidCode(code))
            return ResultDomain<Bank>.Error(Message(nameof(Code), CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT));

        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain<Bank>.Error(Message(nameof(Name), CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED));

        return ResultDomain<Bank>.Ok(new Bank { Code = code.Trim(), Name = name.Trim() });
    }

    private static bool IsValidCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) && code.Length == 4 && code.All(char.IsDigit);

    private static MessageItem Message(string property, string code) =>
        new() { Property = property, Code = code };
}