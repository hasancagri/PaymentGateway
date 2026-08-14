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

// ---- Commission.Api (024 komisyon politikaları) ----

/// <summary>RatePercent ondalık orandır (0.02 = %2, tavan 0.20); FixedFee TL (tavan 100).</summary>
public record CreateCommissionPolicyRequest(Guid MerchantId, decimal RatePercent, decimal FixedFee);

public class CommissionPoliciesResponse
{
    public List<CommissionPolicyItem> Policies { get; set; } = new();
}

public class CommissionPolicyItem
{
    public Guid PolicyId { get; set; }
    public Guid MerchantId { get; set; }
    public decimal RatePercent { get; set; }
    public decimal FixedFee { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; }
}

public class CommissionPolicyResult
{
    public Guid PolicyId { get; set; }
    public Guid MerchantId { get; set; }
    public decimal RatePercent { get; set; }
    public decimal FixedFee { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class CommissionPolicyStatusResult
{
    public Guid MerchantId { get; set; }
    public string Status { get; set; } = string.Empty;
}

// ---- Merchant.Api (029 kayıt başvuruları) ----

public class RegisterRequestsResponse
{
    public List<RegisterRequestItem> Requests { get; set; } = new();
}

public class RegisterRequestItem
{
    public Guid RequestId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string GsmNumber { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string ContactSurname { get; set; } = string.Empty;
    public string? RejectReason { get; set; }
    public Guid? MerchantId { get; set; }
    public DateTime CreatedTime { get; set; }
}

public class ApproveRegisterResult
{
    public Guid RequestId { get; set; }
    public Guid MerchantId { get; set; }
}

public class RejectRegisterResult
{
    public Guid RequestId { get; set; }
    public string Status { get; set; } = string.Empty;
}

// 023 GetMerchantResponse aynası; MerchantKey dev kararıyla açık döner (redeem modeli gelince kalkar).
public class MerchantDetail
{
    public Guid MerchantId { get; set; }
    public string MerchantKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string GsmNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string ContactSurname { get; set; } = string.Empty;
    public string? IdentityNumber { get; set; }
    public string? TaxOffice { get; set; }
    public string? TaxNumber { get; set; }
    public string? LegalCompanyTitle { get; set; }
    public string? SubMerchantKey { get; set; }
    public DateTime CreatedTime { get; set; }
}

public class MerchantsResponse
{
    public List<MerchantListItem> Merchants { get; set; } = new();
}

public class MerchantListItem
{
    public Guid MerchantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
