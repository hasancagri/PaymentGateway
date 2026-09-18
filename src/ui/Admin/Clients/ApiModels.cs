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

// ---- Merchant.Api (044 hassas-veri sayfası — kalan TEK sözleşme; CRUD modelleri söküldü) ----

// 044: hassas-veri sayfası sözleşmesi (GetMerchantSensitive/UpdateMerchantSensitive aynası).
public class MerchantSensitiveDetail
{
    public Guid MerchantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string GsmNumber { get; set; } = string.Empty;
    public string? IdentityNumber { get; set; }
    public string Iban { get; set; } = string.Empty;
    public string? TaxNumber { get; set; }
}

public record UpdateMerchantSensitiveRequest(
    string Email,
    string GsmNumber,
    string? IdentityNumber,
    string Iban,
    string? TaxNumber);