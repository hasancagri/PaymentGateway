using Merchant.Api.Domains.RegisterRequests;

namespace Merchant.Api.Tests;

// 029: RegisterRequest aggregate davranış testleri — saf domain (DB/HTTP/host yok).
// Geçerli TR IBAN örneği: TR330006100519786457841326 (mod-97 = 1).
public class RegisterRequestTests
{
    private const string ValidIban = "TR330006100519786457841326";

    private static ResultDomain<RegisterRequest> SubmitValid(
        MerchantType type = MerchantType.Personal,
        string name = "Kolay Fırsat",
        string email = "iletisim@kolayfirsat.com",
        string gsmNumber = "+905551112233",
        string address = "İstanbul",
        string iban = ValidIban,
        string contactName = "Ahmet",
        string contactSurname = "Yılmaz",
        string? identityNumber = "11111111110",
        string? taxOffice = null,
        string? taxNumber = null,
        string? legalCompanyTitle = null)
        => RegisterRequest.Submit(
            type, name, email, gsmNumber, address, iban, contactName, contactSurname,
            identityNumber, taxOffice, taxNumber, legalCompanyTitle);

    // --- Submit: üç tip geçerli ---

    [Fact]
    public void Submit_PersonalGecerliAlanlarla_PendingDogar()
    {
        var result = SubmitValid();

        Assert.True(result.IsSuccess);
        var request = result.Data!;
        Assert.NotEqual(Guid.Empty, request.Id);
        Assert.Equal(RegisterRequestStatus.Pending, request.Status);
        Assert.Null(request.MerchantId);
        Assert.Null(request.RejectReason);
    }

    [Fact]
    public void Submit_PrivateCompanyGecerliAlanlarla_Basarili()
    {
        var result = SubmitValid(
            type: MerchantType.PrivateCompany,
            identityNumber: "11111111110",
            taxOffice: "Beşiktaş VD",
            legalCompanyTitle: "Kolay Fırsat Şahıs Şirketi");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Submit_LimitedOrJointStockCompanyGecerliAlanlarla_Basarili()
    {
        var result = SubmitValid(
            type: MerchantType.LimitedOrJointStockCompany,
            identityNumber: null,
            taxOffice: "Beşiktaş VD",
            taxNumber: "1234567890",
            legalCompanyTitle: "Kolay Fırsat A.Ş.");

        Assert.True(result.IsSuccess);
    }

    // --- Submit: tip-uyum matrisi ---

    [Fact]
    public void Submit_PersonalKimlikNoYok_Hata()
    {
        var result = SubmitValid(identityNumber: null);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages, m => m.Property == nameof(RegisterRequest.IdentityNumber));
    }

    [Fact]
    public void Submit_PrivateCompanyVergiDairesiYok_Hata()
    {
        var result = SubmitValid(
            type: MerchantType.PrivateCompany,
            identityNumber: "11111111110",
            taxOffice: null,
            legalCompanyTitle: "Unvan");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages, m => m.Property == nameof(RegisterRequest.TaxOffice));
    }

    [Fact]
    public void Submit_PrivateCompanyUnvanYok_Hata()
    {
        var result = SubmitValid(
            type: MerchantType.PrivateCompany,
            identityNumber: "11111111110",
            taxOffice: "Beşiktaş VD",
            legalCompanyTitle: null);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages, m => m.Property == nameof(RegisterRequest.LegalCompanyTitle));
    }

    [Fact]
    public void Submit_LimitedVergiNoYok_Hata()
    {
        var result = SubmitValid(
            type: MerchantType.LimitedOrJointStockCompany,
            taxOffice: "Beşiktaş VD",
            taxNumber: null,
            legalCompanyTitle: "Kolay Fırsat A.Ş.");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages, m => m.Property == nameof(RegisterRequest.TaxNumber));
    }

    // --- Submit: biçim doğrulamaları ---

    [Fact]
    public void Submit_GecersizEposta_Hata()
    {
        var result = SubmitValid(email: "gecersiz-eposta");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages, m => m.Property == nameof(RegisterRequest.Email));
    }

    [Fact]
    public void Submit_GecersizIbanMod97_Hata()
    {
        // Biçim doğru (TR + 24 hane) ama mod-97 tutmaz.
        var result = SubmitValid(iban: "TR330006100519786457841327");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages, m => m.Property == nameof(RegisterRequest.Iban));
    }

    [Fact]
    public void Submit_BoslukluIban_NormalizeEdilirVeGecer()
    {
        var result = SubmitValid(iban: "tr33 0006 1005 1978 6457 8413 26");

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidIban, result.Data!.Iban);
    }

    [Fact]
    public void Submit_ZorunluAlanBos_Hata()
    {
        var result = SubmitValid(name: " ");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages, m => m.Property == nameof(RegisterRequest.Name));
    }

    // --- Statü makinesi ---

    [Fact]
    public void Approve_Pending_ApprovedVeMerchantIdBaglanir()
    {
        var request = SubmitValid().Data!;
        var merchantId = Guid.NewGuid();

        var result = request.Approve(merchantId);

        Assert.True(result.IsSuccess);
        Assert.Equal(RegisterRequestStatus.Approved, request.Status);
        Assert.Equal(merchantId, request.MerchantId);
    }

    [Fact]
    public void Reject_Pending_RejectedVeNedenSaklanir()
    {
        var request = SubmitValid().Data!;

        var result = request.Reject("Evrak eksik");

        Assert.True(result.IsSuccess);
        Assert.Equal(RegisterRequestStatus.Rejected, request.Status);
        Assert.Equal("Evrak eksik", request.RejectReason);
    }

    [Fact]
    public void Reject_BosNeden_Hata()
    {
        var request = SubmitValid().Data!;

        var result = request.Reject("  ");

        Assert.False(result.IsSuccess);
        Assert.Equal(RegisterRequestStatus.Pending, request.Status);
        Assert.Null(request.RejectReason);
    }

    [Fact]
    public void Approve_ApprovedUstune_HataVeDurumKorunur()
    {
        var request = SubmitValid().Data!;
        var firstMerchantId = Guid.NewGuid();
        request.Approve(firstMerchantId);

        var result = request.Approve(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(RegisterRequestStatus.Approved, request.Status);
        Assert.Equal(firstMerchantId, request.MerchantId);
    }

    [Fact]
    public void Reject_ApprovedUstune_HataVeDurumKorunur()
    {
        var request = SubmitValid().Data!;
        request.Approve(Guid.NewGuid());

        var result = request.Reject("Geç kaldı");

        Assert.False(result.IsSuccess);
        Assert.Equal(RegisterRequestStatus.Approved, request.Status);
        Assert.Null(request.RejectReason);
    }

    [Fact]
    public void Approve_RejectedUstune_HataVeDurumKorunur()
    {
        var request = SubmitValid().Data!;
        request.Reject("Evrak eksik");

        var result = request.Approve(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(RegisterRequestStatus.Rejected, request.Status);
        Assert.Null(request.MerchantId);
    }
}
