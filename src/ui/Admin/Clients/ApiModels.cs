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

// 019 (FR-013): merchant komisyon upsert/finalize modelleri SÖKÜLDÜ — komisyon yalnız teklif
// kabulüyle yazılır; admin ekranı salt-okuma + teklif durumu gösterir.

public class MerchantCommissionsResponse
{
    public List<MerchantCommissionItem> Items { get; set; } = new();
}

/// <summary>019 US5 — merchant'ın SON komisyon teklifinin durumu (Commission.Api /commission-proposals/status).</summary>
public class CommissionProposalStatusModel
{
    public string Status { get; set; } = "None";
    public Guid? ProposalId { get; set; }
    public DateTime? DecidedTime { get; set; }
    public string? RejectReason { get; set; }
    public int RowCount { get; set; }
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

