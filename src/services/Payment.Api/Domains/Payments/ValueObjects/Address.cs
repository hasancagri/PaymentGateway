namespace Payment.Api.Domains.Payments.ValueObjects;

/// <summary>
/// Çekim adresi — değer nesnesi (035). Bağımsız girdi DEĞİL: alıcıdan türetilir (sevk = fatura;
/// bugünkü <c>BuildAddress(buyer)</c> davranışı korunur). iyzico'yu bilmez; handler SDK
/// <c>ChargePayment</c> slice'ının nested wire Address'ine map'ler. Kalıcı değil (charge-anı).
/// </summary>
public sealed class Address
{
    private Address()
    {
    }

    public string ContactName { get; private init; } = string.Empty;
    public string City { get; private init; } = string.Empty;
    public string Country { get; private init; } = string.Empty;
    public string Description { get; private init; } = string.Empty;

    /// <summary>
    /// Alıcıdan adres türetir: contactName = ad + soyad, şehir/ülke/açıklama = alıcı alanları.
    /// (Türetme kapsül içinde; ayrı input yok — davranış-koruma.)
    /// </summary>
    public static ResultDomain<Address> FromBuyer(Buyer buyer)
    {
        if (buyer is null)
            return ResultDomain<Address>.Error(new MessageItem
            { Property = nameof(Address), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });

        return ResultDomain<Address>.Ok(new Address
        {
            ContactName = $"{buyer.Name} {buyer.Surname}",
            City = buyer.City,
            Country = buyer.Country,
            Description = buyer.RegistrationAddress
        });
    }
}
