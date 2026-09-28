namespace Shared;

// MCP tool DESCRIPTION'larının TEK kaynağı (003 — doğal-dil routing standardı). McpToolNames.cs
// deseninin eşi: attribute'lar [Description(McpToolDescriptions.X.Y)] ile buraya referans verir,
// inline string TAŞIMAZ. Standart: eylem-önce cümle → en az bir Türkçe tetikleyici ifade
// ("'...' gibi istekler için") → opsiyonel girdi/çıktı → governance/PII kısıtı EN SONDA.
// Prose uyum-prefix'i (YÖNETİM:) YOK — yönetim sinyali admin_ isminden gelir. Büyük-harf bağırma YOK.
// Kanonik standart: AgentPlatform/docs/mcp-tool-description-standard.md.
// Sınıf yapısı McpToolNames ile birebir hizalı (aynı sınıf + üye adı).

public static class McpToolDescriptions
{
    public static class MerchantAdminTools
    {
        public const string GetMerchants =
            "Merchant listesini döndürür (opsiyonel statü filtresi: Active | Passive | Suspended; boşsa tümü). " +
            "\"Satıcıları listele\", \"merchant'ları göster\", \"aktif satıcılar\" gibi istekler için. " +
            "Not: kişisel/finansal veri ve sır alanları yanıtta yer almaz.";

        public const string GetMerchant =
            "Tekil merchant detayını döndürür (statü, tip, ad, adres, iletişim adı, vergi dairesi, unvan). " +
            "\"Şu merchant'ın detayı\", \"satıcı bilgisini göster\" gibi istekler için. " +
            "Not: kişisel/finansal veri ve sır alanları yanıtta yok; onlar yalnız Admin hassas-veri sayfasından yönetilir.";

        public const string ActivateMerchant =
            "Merchant'ı Active durumuna alır. " +
            "\"Satıcıyı aktifleştir\", \"merchant'ı aktif yap\" gibi istekler için. " +
            "Zaten Active ise hata dönmez, changed=false ile idempotent no-op sonucu döner.";

        public const string DeactivateMerchant =
            "Merchant'ı Passive durumuna alır. " +
            "\"Satıcıyı pasifleştir\", \"merchant'ı devre dışı bırak\" gibi istekler için. " +
            "Zaten Passive ise hata dönmez, changed=false ile idempotent no-op sonucu döner.";

        public const string SuspendMerchant =
            "Merchant'ı Suspended durumuna alır. " +
            "\"Satıcıyı askıya al\", \"merchant'ı süspend et\" gibi istekler için. " +
            "Zaten Suspended ise hata dönmez, changed=false ile idempotent no-op sonucu döner.";

        public const string UpdateMerchant =
            "Merchant'ın hassas-dışı alanlarını günceller (tip, ad, adres, iletişim adı/soyadı, vergi dairesi, unvan). " +
            "\"Satıcının unvanını değiştir\", \"merchant adresini güncelle\" gibi istekler için. " +
            "Tip: Personal | PrivateCompany | LimitedOrJointStockCompany. Not: kişisel/finansal alanlar bu tool'dan " +
            "değiştirilemez; onlar yalnız Admin hassas-veri sayfasından yönetilir.";

        public const string GetPendingRegistrations =
            "Admin onayı bekleyen (Pending) kayıt başvurularını döndürür — başvuru Id, işletme adı/tipi/adresi, " +
            "iletişim adı ve tarih. " +
            "\"Bekleyen başvuruları göster\", \"onay bekleyen kayıtlar\" gibi istekler için. " +
            "Not: başvuru sahibinin kişisel verisi yanıtta yer almaz.";

        public const string ApproveRegistration =
            "Pending başvuruyu onaylar: yeni Active merchant doğar, merchantId döner. " +
            "\"Başvuruyu onayla\", \"satıcı kaydını kabul et\" gibi istekler için. " +
            "Başvuru zaten terminal statüdeyse (Approved/Rejected) INVALID_OPERATION_ERROR döner.";

        public const string RejectRegistration =
            "Pending başvuruyu reddeder — neden zorunludur, başvuruda saklanır. " +
            "\"Başvuruyu reddet\", \"kaydı geri çevir\" gibi istekler için. " +
            "Başvuru zaten terminal statüdeyse INVALID_OPERATION_ERROR döner; neden boşsa COMMON_MESSAGE_VALUE_IS_REQUIRED döner.";

        public const string ResendCredentialLink =
            "Merchant'ın erişim bilgileri (MerchantId + MerchantKey) için yeni tek kullanımlık teslim bağlantısı üretir " +
            "ve merchant e-postasına mailler; yaşayan eski bağlantılar geçersiz olur. " +
            "\"Credential linkini yeniden gönder\", \"erişim bilgisi bağlantısını tekrar yolla\" gibi istekler için " +
            "(teslim maili kaybolduğunda ya da süresi dolduğunda). " +
            "Not: key/link yanıtta dönmez — teslim yalnız mail + tek gösterimlik sayfa yoluyladır.";

        public const string RequestSensitiveLink =
            "Bir merchant'ın hassas kişisel/finansal alanlarını (Email/GSM/TCKN/IBAN/vergi no) görüntülemek ve düzenlemek " +
            "için store'un hosted ekranına süreli + tek kullanımlık link üretir. " +
            "\"Merchant hassas veri linki oluştur\", \"satıcının IBAN/TCKN düzenleme ekranı\" gibi istekler için. " +
            "Yanıt {url, expiresAt, message}; sohbete yalnız linki düşür. Not: hassas veriyi sohbetten isteme ve asla " +
            "sohbete yazma — değerler yalnız tarayıcıdaki sayfada görünür.";
    }

    public static class CommissionAdminTools
    {
        public const string GetCommissionPolicy =
            "Merchant'ın marj politikasını özetler (ör. \"%2.5 + 0.10 TL, 3 kademe\"). " +
            "\"Komisyon politikasını göster\", \"merchant'ın marjı ne\" gibi istekler için. " +
            "Politika tanımlı değilse hata dönmez, hasPolicy=false + \"Tanımlı politika yok\" döner. " +
            "Not: salt-okuma; oluşturma admin_create_commission_policy, marj güncelleme admin_update_commission_margin ile yapılır.";

        public const string CreatePolicy =
            "Merchant'a komisyon (marj) politikası oluşturur. " +
            "\"Komisyon politikası oluştur\", \"merchant'a marj tanımla\" gibi istekler için. " +
            "tiers: kademe listesi — fromAmount TL (ilki 0), ratePercent ondalık oran (0.025 = %2,5), fixedFee TL. " +
            "Merchant'ın zaten aktif politikası varsa duplicate hatası döner (önce statüsünü Passive yap veya marjı güncelle).";

        public const string UpdateMargin =
            "Merchant'ın komisyon politikasının marj tarifesini günceller. " +
            "\"Marjı güncelle\", \"komisyon oranını değiştir\" gibi istekler için. " +
            "Tam kademe seti gönderilir (kısmi patch yok) — tablo bütünüyle yenisiyle değişir. " +
            "Politika yoksa kayıt-bulunamadı hatası döner.";

        public const string ChangeStatus =
            "Merchant'ın komisyon politikasının statüsünü değiştirir (Active | Passive). " +
            "\"Komisyon politikasını pasifleştir\", \"marj politikası statüsünü değiştir\" gibi istekler için. " +
            "Pasif politika hesaplamada yok sayılır. Aynı statüye geçiş hata dönmez (idempotent no-op). " +
            "Politika yoksa kayıt-bulunamadı hatası döner.";
    }
}
