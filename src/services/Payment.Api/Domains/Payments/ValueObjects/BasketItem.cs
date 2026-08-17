namespace Payment.Api.Domains.Payments.ValueObjects;

/// <summary>
/// Sepet kalemi — değer nesnesi (035). iyzico wire tipinden yapısal uyarlama; iyzico serileştirmeyi
/// bilmez (o SDK wire tipinde). Kalıcı değil (charge-anı). Handler SDK
/// <c>ChargePayment</c> slice'ının nested wire BasketItem'ına map'ler (ItemType config'ten, ör. "PHYSICAL").
/// </summary>
public sealed class BasketItem
{
    private BasketItem()
    {
    }

    public string Id { get; private init; } = string.Empty;
    public string Name { get; private init; } = string.Empty;
    public string Category1 { get; private init; } = string.Empty;
    public decimal Price { get; private init; }

    /// <summary>
    /// Sepet kalemi VO'su üretir. Zorunlu alanlar boş olamaz; fiyat > 0. Geçersizse <c>Error</c>.
    /// </summary>
    public static ResultDomain<BasketItem> Create(string id, string name, string category1, decimal price)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(category1))
            return ResultDomain<BasketItem>.Error(new MessageItem
            { Property = nameof(BasketItem), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });

        if (price <= 0)
            return ResultDomain<BasketItem>.Error(new MessageItem
            { Property = nameof(Price), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT });

        return ResultDomain<BasketItem>.Ok(new BasketItem
        {
            Id = id,
            Name = name,
            Category1 = category1,
            Price = price
        });
    }
}
