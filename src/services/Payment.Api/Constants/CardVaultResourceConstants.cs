namespace Payment.Api.Constants;

// 040: Hosted kart saklama (CardVault) hata kodları (Result pattern: Code sabittir, serbest metin yasak).
public static class CardVaultResourceConstants
{
    public static readonly string CARD_SESSION_FAILED = "CARD_SESSION_FAILED";       // CF init/retrieve başarısız
    public static readonly string CARD_NOT_FOUND = "CARD_NOT_FOUND";                 // cardHandle userHandle kümesinde yok
    public static readonly string PROVIDER_UNAVAILABLE = "PROVIDER_UNAVAILABLE";     // sağlayıcı erişilemez/timeout
    public static readonly string CARD_SESSION_EXPIRED = "CARD_SESSION_EXPIRED";     // oturum süre-dolmuş/tüketilmiş
    public static readonly string CARD_TENANT_MISMATCH = "CARD_TENANT_MISMATCH";     // userHandle çağıran merchant'a ait değil (FR-012)
}
