namespace Admin.Clients;

/// <summary>API doğrulama mesajı (FeatureObjectResultModel.Messages öğesi).</summary>
public class ApiMessage
{
    public string? Property { get; set; }
    public string? Code { get; set; }
}

/// <summary>Hata zarfı (isSuccess=false + messages). Başarı yanıtı Data'yı düz döner.</summary>
public class ErrorEnvelope
{
    public bool IsSuccess { get; set; }
    public List<ApiMessage>? Messages { get; set; }
}

/// <summary>İstemci sonucu: başarı → Data; hata → Messages (kullanıcıya gösterilecek kodlar).</summary>
public class ApiResult<T>
{
    public bool IsSuccess { get; private init; }
    public T? Data { get; private init; }
    public List<ApiMessage> Messages { get; private init; } = new();

    public static ApiResult<T> Ok(T? data) => new() { IsSuccess = true, Data = data };
    public static ApiResult<T> Fail(List<ApiMessage> messages) => new() { IsSuccess = false, Messages = messages };
}

// ---- Merchant.Api ----

public record CreateMerchantRequest(
    string Name,
    string Email,
    string Phone,
    string CountryCode,
    string CityCode,
    string Mcc,
    string WebhookUrl);

public class IdResult
{
    public Guid Id { get; set; }
}

public class MerchantDetail
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string? CountryName { get; set; }
    public string CityCode { get; set; } = string.Empty;
    public string? CityName { get; set; }
    public string Mcc { get; set; } = string.Empty;
    public string? MccName { get; set; }
    public string WebhookUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; }
}

public class MerchantsResponse
{
    public List<MerchantListItem> Merchants { get; set; } = new();
}

public class MerchantListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Mcc { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

// ---- Commission.Api ----

public class CriteriaDto
{
    public string CardBrand { get; set; } = string.Empty;
    public string CardType { get; set; } = string.Empty;
    public string TransactionRegion { get; set; } = string.Empty;
    public int InstallmentCount { get; set; }
}

public record CreateBankCommissionRequest(string BankCode, CriteriaDto Criteria, decimal Rate);

public class BankCommissionsResponse
{
    public List<BankCommissionItem> Items { get; set; } = new();
}

public class BankCommissionItem
{
    public Guid Id { get; set; }
    public string BankCode { get; set; } = string.Empty;
    public CriteriaDto Criteria { get; set; } = new();
    public decimal Rate { get; set; }
}

public record CreateMerchantCommissionRequest(Guid MerchantId, Guid BankCommissionId, decimal Rate);

public record UpdateMerchantCommissionRequest(decimal Rate);

public class MerchantCommissionsResponse
{
    public List<MerchantCommissionItem> Items { get; set; } = new();
}

public class MerchantCommissionItem
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public Guid BankCommissionId { get; set; }
    public string BankCode { get; set; } = string.Empty;
    public CriteriaDto Criteria { get; set; } = new();
    public decimal Rate { get; set; }
}