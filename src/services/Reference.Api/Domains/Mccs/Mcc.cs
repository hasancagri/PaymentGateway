namespace Reference.Api.Domains.Mccs;

/// <summary>
/// Merchant Category Code referans kaydı. İş anahtarı: Code (tam 4 hane). Statik
/// <see cref="Create"/> + invariant (4-hane kod, ad boş değil).
/// </summary>
public class Mcc : AggregateRoot
{
    private Mcc()
    {
    }

    /// <summary>MCC kodu (tam 4 hane). İş anahtarı, immutable.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>MCC adı. Boş değil.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>MCC kaydı üretir; kod 4-hane değilse veya ad boşsa hata Result döner.</summary>
    /// <remarks>Handler: ReferenceSeeder</remarks>
    public static ResultDomain<Mcc> Create(string code, string name)
    {
        if (!IsValidCode(code))
            return ResultDomain<Mcc>.Error(Message(nameof(Code), CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT));

        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain<Mcc>.Error(Message(nameof(Name), CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED));

        return ResultDomain<Mcc>.Ok(new Mcc { Code = code.Trim(), Name = name.Trim() });
    }

    private static bool IsValidCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) && code.Length == 4 && code.All(char.IsDigit);

    private static MessageItem Message(string property, string code) =>
        new() { Property = property, Code = code };
}