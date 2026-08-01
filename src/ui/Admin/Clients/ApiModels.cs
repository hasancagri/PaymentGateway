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

/// <summary>Kriter enum seçenekleri (domain enum'larından; UI kopyalamaz).</summary>
public class CriteriaOptions
{
    public List<string> CardBrands { get; set; } = new();
    public List<string> CardTypes { get; set; } = new();
    public List<string> TransactionRegions { get; set; } = new();
}

// Bank referans aggregate

// Ad ve kod katalogdan gelir; istekler yalnız seçim + taksit/aktiflik taşır.
public record CreateBankRequest(string Code, List<int> SupportedInstallments);

public record UpdateBankRequest(bool IsActive, List<int> SupportedInstallments);

public class BankCatalogResponse
{
    public List<BankCatalogItem> Items { get; set; } = new();
}

public class BankCatalogItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class BanksResponse
{
    public List<BankListItem> Items { get; set; } = new();
}

public class BankListItem
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<int> SupportedInstallments { get; set; } = new();
    public bool IsActive { get; set; }
}

public class BankDetail
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<int> SupportedInstallments { get; set; } = new();
    public bool IsActive { get; set; }
}

public class CodeResult
{
    public string Code { get; set; } = string.Empty;
}

// Toplu komisyon (grid kaydı)

public record BulkBankCommissionItem(CriteriaDto Criteria, decimal Rate);

public record BulkBankCommissionsRequest(string BankCode, List<BulkBankCommissionItem> Items);

public class BulkBankCommissionsResult
{
    public int Created { get; set; }
    public int Updated { get; set; }
}

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

public record CreateMerchantCommissionRequest(Guid MerchantId, CriteriaDto Criteria, decimal Rate);

public record UpdateMerchantCommissionRequest(decimal Rate);

// Toplu merchant komisyonu (grid kaydı)

public record MerchantCommissionBulkItem(CriteriaDto Criteria, decimal Rate);

public record BulkUpsertMerchantCommissionsRequest(Guid MerchantId, List<MerchantCommissionBulkItem> Items);

public class BulkUpsertResult
{
    public int Created { get; set; }
    public int Updated { get; set; }
}

public class MerchantCommissionsResponse
{
    public List<MerchantCommissionItem> Items { get; set; } = new();
}

/// <summary>Enriched grid satırı: merchant oranı + banka aralığı (min/max) + tavan-altı işareti (read-time).</summary>
public class MerchantCommissionItem
{
    public Guid? Id { get; set; }
    public Guid MerchantId { get; set; }
    public CriteriaDto Criteria { get; set; } = new();
    public decimal? Rate { get; set; }
    public decimal? BankMin { get; set; }
    public decimal? BankMax { get; set; }
    public bool BelowBankCeiling { get; set; }
    public bool IsMissing { get; set; }
}