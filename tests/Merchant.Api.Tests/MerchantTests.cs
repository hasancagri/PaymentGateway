namespace Merchant.Api.Tests;

// 023: Merchant aggregate davranış testleri — saf domain (DB/HTTP/host yok).
// Geçerli TR IBAN örneği: TR330006100519786457841326 (mod-97 = 1).
public class MerchantTests
{
    private const string ValidIban = "TR330006100519786457841326";

    private static ResultDomain<Domains.Merchants.Merchant> CreateValid(
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
        => Domains.Merchants.Merchant.Create(
            type, name, email, gsmNumber, address, iban, contactName, contactSurname,
            identityNumber, taxOffice, taxNumber, legalCompanyTitle);

    // --- Oluşturma (üç tip) + MerchantKey ---

    [Fact]
    public void Create_PersonalGecerliAlanlarla_BasariliVeActiveDogar()
    {
        var result = CreateValid();

        Assert.True(result.IsSuccess);
        var merchant = result.Data!;
        Assert.NotEqual(Guid.Empty, merchant.Id);
        Assert.Equal(MerchantStatus.Active, merchant.Status);
        Assert.Equal(MerchantType.Personal, merchant.Type);
        Assert.StartsWith("mk_", merchant.MerchantKey);
        Assert.Null(merchant.SubMerchantKey);
    }

    [Fact]
    public void Create_PrivateCompanyGecerliAlanlarla_Basarili()
    {
        var result = CreateValid(
            type: MerchantType.PrivateCompany,
            identityNumber: "11111111110",
            taxOffice: "Kadıköy",
            legalCompanyTitle: "Ahmet Yılmaz Şahıs Şirketi");

        Assert.True(result.IsSuccess);
        Assert.Equal(MerchantType.PrivateCompany, result.Data!.Type);
    }

    [Fact]
    public void Create_LimitedOrJointStockGecerliAlanlarla_Basarili()
    {
        var result = CreateValid(
            type: MerchantType.LimitedOrJointStockCompany,
            identityNumber: null,
            taxOffice: "Beşiktaş",
            taxNumber: "1234567890",
            legalCompanyTitle: "Kolay Fırsat A.Ş.");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_HerCagrida_BenzersizIdVeMerchantKey()
    {
        var first = CreateValid().Data!;
        var second = CreateValid().Data!;

        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.MerchantKey, second.MerchantKey);
    }

    // --- Tip-uyum matrisi ---

    [Fact]
    public void Create_PersonalKimlikNosuz_TipUyumReddi()
    {
        var result = CreateValid(identityNumber: null);

        Assert.False(result.IsSuccess);
        Assert.Equal(nameof(Domains.Merchants.Merchant.IdentityNumber), result.Messages![0].Property);
        Assert.Equal(CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED, result.Messages[0].Code);
    }

    [Fact]
    public void Create_PersonalVergiAlanlariBos_Basarili()
    {
        // Spec senaryo 2: şahısta vergi alanları zorunlu değil.
        var result = CreateValid(taxOffice: null, taxNumber: null, legalCompanyTitle: null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.TaxOffice);
        Assert.Null(result.Data.TaxNumber);
        Assert.Null(result.Data.LegalCompanyTitle);
    }

    [Theory]
    [InlineData(null, "Kadıköy", "Unvan", nameof(Domains.Merchants.Merchant.IdentityNumber))]
    [InlineData("11111111110", null, "Unvan", nameof(Domains.Merchants.Merchant.TaxOffice))]
    [InlineData("11111111110", "Kadıköy", null, nameof(Domains.Merchants.Merchant.LegalCompanyTitle))]
    public void Create_PrivateCompanyZorunluAlanEksik_TipUyumReddi(
        string? identityNumber, string? taxOffice, string? legalCompanyTitle, string beklenenAlan)
    {
        var result = CreateValid(
            type: MerchantType.PrivateCompany,
            identityNumber: identityNumber,
            taxOffice: taxOffice,
            legalCompanyTitle: legalCompanyTitle);

        Assert.False(result.IsSuccess);
        Assert.Equal(beklenenAlan, result.Messages![0].Property);
    }

    [Fact]
    public void Create_PrivateCompanyVergiNosuz_Basarili()
    {
        // İyzico matrisi: şahıs şirketinde TaxNumber opsiyonel.
        var result = CreateValid(
            type: MerchantType.PrivateCompany,
            identityNumber: "11111111110",
            taxOffice: "Kadıköy",
            taxNumber: null,
            legalCompanyTitle: "Ahmet Yılmaz Şahıs Şirketi");

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(null, "1234567890", "Unvan", nameof(Domains.Merchants.Merchant.TaxOffice))]
    [InlineData("Beşiktaş", null, "Unvan", nameof(Domains.Merchants.Merchant.TaxNumber))]
    [InlineData("Beşiktaş", "1234567890", null, nameof(Domains.Merchants.Merchant.LegalCompanyTitle))]
    public void Create_LimitedOrJointStockZorunluAlanEksik_TipUyumReddi(
        string? taxOffice, string? taxNumber, string? legalCompanyTitle, string beklenenAlan)
    {
        var result = CreateValid(
            type: MerchantType.LimitedOrJointStockCompany,
            identityNumber: null,
            taxOffice: taxOffice,
            taxNumber: taxNumber,
            legalCompanyTitle: legalCompanyTitle);

        Assert.False(result.IsSuccess);
        Assert.Equal(beklenenAlan, result.Messages![0].Property);
    }

    // --- Biçim doğrulamaları ---

    [Theory]
    [InlineData("TR00INVALID")]
    [InlineData("TR330006100519786457841327")] // mod-97 tutmaz (son hane bozuk)
    [InlineData("DE44500105175407324931")]     // TR değil
    public void Create_BozukIban_BicimReddi(string iban)
    {
        var result = CreateValid(iban: iban);

        Assert.False(result.IsSuccess);
        Assert.Equal(nameof(Domains.Merchants.Merchant.Iban), result.Messages![0].Property);
        Assert.Equal(CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT, result.Messages[0].Code);
    }

    [Fact]
    public void Create_BoslukluKucukHarfliIban_NormalizeEdilir()
    {
        var result = CreateValid(iban: "tr33 0006 1005 1978 6457 8413 26");

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidIban, result.Data!.Iban);
    }

    [Theory]
    [InlineData("bozuk")]
    [InlineData("a@b")]
    [InlineData("@alan.com")]
    public void Create_BozukEposta_BicimReddi(string email)
    {
        var result = CreateValid(email: email);

        Assert.False(result.IsSuccess);
        Assert.Equal(nameof(Domains.Merchants.Merchant.Email), result.Messages![0].Property);
        Assert.Equal(CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT, result.Messages[0].Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ZorunluAlanBos_Red(string name)
    {
        var result = CreateValid(name: name);

        Assert.False(result.IsSuccess);
        Assert.Equal(nameof(Domains.Merchants.Merchant.Name), result.Messages![0].Property);
        Assert.Equal(CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED, result.Messages[0].Code);
    }

    // --- UpdateDetails ---

    [Fact]
    public void UpdateDetails_GecerliAlanlar_KimlikVeKeyDegismez()
    {
        var merchant = CreateValid().Data!;
        var id = merchant.Id;
        var key = merchant.MerchantKey;

        var result = merchant.UpdateDetails(
            MerchantType.LimitedOrJointStockCompany, "Yeni Ad", "yeni@ornek.com", "+905550000000",
            "Ankara", ValidIban, "Mehmet", "Demir",
            null, "Çankaya", "9876543210", "Yeni Unvan A.Ş.");

        Assert.True(result.IsSuccess);
        Assert.Equal(id, merchant.Id);
        Assert.Equal(key, merchant.MerchantKey);
        Assert.Equal(MerchantStatus.Active, merchant.Status);
        Assert.Null(merchant.SubMerchantKey);
        Assert.Equal("Yeni Ad", merchant.Name);
        Assert.Equal(MerchantType.LimitedOrJointStockCompany, merchant.Type);
    }

    [Fact]
    public void UpdateDetails_TipUyumIhlali_RedVeAlanlarDegismez()
    {
        var merchant = CreateValid().Data!;
        var eskiAd = merchant.Name;

        var result = merchant.UpdateDetails(
            MerchantType.LimitedOrJointStockCompany, "Yeni Ad", "yeni@ornek.com", "+905550000000",
            "Ankara", ValidIban, "Mehmet", "Demir",
            null, null, null, null); // sermaye şirketi vergi bilgisi/unvansız

        Assert.False(result.IsSuccess);
        Assert.Equal(eskiAd, merchant.Name); // ihlalde kayıt değişmez (SC-002)
        Assert.Equal(MerchantType.Personal, merchant.Type);
    }

    [Fact]
    public void UpdateDetails_BozukIban_Red()
    {
        var merchant = CreateValid().Data!;

        var result = merchant.UpdateDetails(
            MerchantType.Personal, "Ad", "a@b.com", "+905550000000",
            "Ankara", "TR00INVALID", "Mehmet", "Demir",
            "11111111110", null, null, null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ValidIban, merchant.Iban);
    }

    // --- Statü geçişleri ---

    [Theory]
    [InlineData(MerchantStatus.Passive)]
    [InlineData(MerchantStatus.Suspended)]
    public void ChangeStatus_FarkliStatuye_DegistiVeTrueDoner(MerchantStatus hedef)
    {
        var merchant = CreateValid().Data!;

        var result = merchant.ChangeStatus(hedef);

        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
        Assert.Equal(hedef, merchant.Status);
    }

    [Fact]
    public void ChangeStatus_AyniStatuye_IdempotentFalseDoner()
    {
        var merchant = CreateValid().Data!;

        var result = merchant.ChangeStatus(MerchantStatus.Active);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data); // değişmedi → çağıran event yayınlamaz (R5)
        Assert.Equal(MerchantStatus.Active, merchant.Status);
    }

    [Fact]
    public void ChangeStatus_UcStatuArasiSerbestGecis()
    {
        var merchant = CreateValid().Data!;

        Assert.True(merchant.ChangeStatus(MerchantStatus.Suspended).Data);
        Assert.True(merchant.ChangeStatus(MerchantStatus.Passive).Data);
        Assert.True(merchant.ChangeStatus(MerchantStatus.Active).Data);
        Assert.Equal(MerchantStatus.Active, merchant.Status);
        Assert.True(merchant.IsActive);
    }

    [Fact]
    public void ChangeStatus_ActiveDisiStatude_IsActiveFalse()
    {
        var merchant = CreateValid().Data!;

        merchant.ChangeStatus(MerchantStatus.Suspended);

        Assert.False(merchant.IsActive);
    }
}