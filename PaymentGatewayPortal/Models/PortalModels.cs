namespace PaymentGatewayPortal.Models;

public class BffResult<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
}

public class MerchantListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Status { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Mcc { get; set; } = string.Empty;
}

public class MerchantDetail
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Status { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Mcc { get; set; } = string.Empty;
}

public record CreateMerchantRequest(string Name, string Email, string Phone, string Country, string City, string Mcc);

public record UpdateMerchantRequest(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    string Country,
    string City,
    string Mcc);

public class MerchantCommissionListItem
{
    public Guid Id { get; set; }
    public Guid BankCommissionId { get; set; }
    public decimal Rate { get; set; }
    public int CardBrand { get; set; }
    public int CardType { get; set; }
    public int TransactionRegion { get; set; }
}

public class BankCommissionListItem
{
    public Guid Id { get; set; }
    public Guid BankId { get; set; }
    public decimal Rate { get; set; }
    public int CardBrand { get; set; }
    public int CardType { get; set; }
    public int TransactionRegion { get; set; }
}

public record DefineMerchantCommissionRequest(
    Guid MerchantId,
    Guid BankCommissionId,
    int CardBrand,
    int CardType,
    int TransactionRegion,
    decimal MerchantRate);