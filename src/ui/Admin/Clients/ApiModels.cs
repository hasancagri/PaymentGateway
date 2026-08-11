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

public class FinalizeResult
{
    public Guid MerchantId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool Activated { get; set; }
    public bool HasSettlementAccount { get; set; }
    public bool CommissionGridReady { get; set; }
    public bool HasReturnUrl { get; set; }
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

// ---- Merchant.Api (013 onboarding — RegisterRequest) ----

public class RegisterRequestsResponse
{
    public List<RegisterRequestListItem> Items { get; set; } = new();
}

public class RegisterRequestListItem
{
    public Guid Id { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; }
}

public record ReviewRequest(string? Note);

public class ApproveRegisterResult
{
    public Guid MerchantId { get; set; }
    public Guid RequestId { get; set; }
}

// ---- Merchant.Api (settlement hesapları) ----

public record CreateSettlementAccountRequest(
    string BankCode,
    string Iban,
    string AccountOwnerName,
    string AccountNo,
    string AccountDescription);

public record UpdateSettlementAccountRequest(
    string BankCode,
    string Iban,
    string AccountOwnerName,
    string AccountNo,
    string AccountDescription);

public record SetSettlementAccountStatusRequest(bool IsActive);

public class SettlementAccountsResponse
{
    public List<SettlementAccountListItem> Accounts { get; set; } = new();
}

/// <summary>Liste satırı (GET /). <c>BankName</c> lookup türevi (bilinmeyen kod → null).</summary>
public class SettlementAccountListItem
{
    public Guid Id { get; set; }
    public string BankCode { get; set; } = string.Empty;
    public string? BankName { get; set; }
    public string Iban { get; set; } = string.Empty;
    public string AccountOwnerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

/// <summary>Tekil ayrıntı (GET /{accountId}).</summary>
public class SettlementAccountDetail
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public string BankCode { get; set; } = string.Empty;
    public string? BankName { get; set; }
    public string Iban { get; set; } = string.Empty;
    public string AccountOwnerName { get; set; } = string.Empty;
    public string AccountNo { get; set; } = string.Empty;
    public string AccountDescription { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; }
}

/// <summary>Durum değişimi yanıtı ({ id, status }).</summary>
public class IdStatusResult
{
    public Guid Id { get; set; }
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

public record FinalizeGridRequest(Guid MerchantId);

public class GridStatusResult
{
    public string Status { get; set; } = string.Empty;
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

// ---- Payment.Api — BinCard katalog (009) ----

/// <summary>Tekil BIN detayı: ham alanlar (enum'lar string ad) + türetilmiş taksit-banka listesi.</summary>
public class BinCardDetail
{
    public string BinNumber { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string CardType { get; set; } = string.Empty;
    public string CardBrand { get; set; } = string.Empty;
    public string CardProgram { get; set; } = string.Empty;
    public bool Commercial { get; set; }
    public List<string> InstallmentBankCodes { get; set; } = new();
}

/// <summary>Liste satırı (taksit-banka türetmesiz).</summary>
public class BinCardListItem
{
    public string BinNumber { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string CardType { get; set; } = string.Empty;
    public string CardBrand { get; set; } = string.Empty;
    public string CardProgram { get; set; } = string.Empty;
    public bool Commercial { get; set; }
}

/// <summary>Sayfalı liste yanıtı (Payment.Api BinCardListResponse karşılığı).</summary>
public class BinCardListResponse
{
    public List<BinCardListItem> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int PageCount { get; set; }
}

/// <summary>Liste filtre + sayfa bağlaması (GET query).</summary>
public class BinCardListFilter
{
    public string? BankCode { get; set; }
    public string? CardProgram { get; set; }
    public string? CardType { get; set; }
    public string? CardBrand { get; set; }
    public bool? Commercial { get; set; }
    public int Page { get; set; } = 1;
}